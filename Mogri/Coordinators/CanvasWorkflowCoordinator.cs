using Mogri.Enums;
using Mogri.Helpers;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Models;
using SkiaSharp;

namespace Mogri.Coordinators;

/// <summary>
/// Coordinates canvas workflow bitmap preparation and patch execution without mutating viewmodel state.
/// </summary>
public sealed class CanvasWorkflowCoordinator : ICanvasWorkflowCoordinator
{
    private const string PngContentType = "image/png";

    private readonly ICanvasActionBitmapService _canvasActionBitmapService;
    private readonly ICanvasBitmapService _canvasBitmapService;
    private readonly IFileService _fileService;
    private readonly IPatchService _patchService;
    private readonly ISegmentationService _segmentationService;

    public CanvasWorkflowCoordinator(
        ICanvasActionBitmapService canvasActionBitmapService,
        ICanvasBitmapService canvasBitmapService,
        IFileService fileService,
        IPatchService patchService,
        ISegmentationService segmentationService)
    {
        _canvasActionBitmapService = canvasActionBitmapService ?? throw new ArgumentNullException(nameof(canvasActionBitmapService));
        _canvasBitmapService = canvasBitmapService ?? throw new ArgumentNullException(nameof(canvasBitmapService));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _patchService = patchService ?? throw new ArgumentNullException(nameof(patchService));
        _segmentationService = segmentationService ?? throw new ArgumentNullException(nameof(segmentationService));
    }

    public Task<string> SaveImageAsync(SKBitmap sourceBitmap)
    {
        ArgumentNullException.ThrowIfNull(sourceBitmap);

        return Task.Run(async () =>
        {
            using var memStream = new MemoryStream();
            using var skiaStream = new SKManagedWStream(memStream);

            sourceBitmap.Encode(skiaStream, SKEncodedImageFormat.Png, 100);

            var fileName = $"CanvasImage-{DateTime.Now.Ticks}.png";
            memStream.Seek(0, SeekOrigin.Begin);
            await _fileService.WriteImageFileToExternalStorageAsync(fileName, memStream, false);

            return fileName;
        });
    }

    public Task<CanvasWorkflowNavigationResult?> CreateImageToImageNavigationAsync(CanvasWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(() =>
        {
            using var rawMaskBitmap = _canvasActionBitmapService.CreateRenderedLayer(request.CanvasActions, request.SourceBitmap.Width, request.SourceBitmap.Height);

            var colorizedBitmap = CreatePreparedWorkflowBitmap(request.SourceBitmap, rawMaskBitmap, request.CanvasUseMode);
            using var ownedColorizedBitmap = colorizedBitmap != null && !ReferenceEquals(colorizedBitmap, request.SourceBitmap)
                ? colorizedBitmap
                : null;

            var payload = ImagePayloadHelper.CreateConstrainedPayload(colorizedBitmap, PngContentType);
            if (payload == null)
            {
                return null;
            }

            return new CanvasWorkflowNavigationResult(CreateWorkflowNavigationParameters(payload, CreateWorkflowMaskImageData(rawMaskBitmap, request)));
        });
    }

    public Task<CanvasWorkflowNavigationResult?> CreateCropNavigationAsync(CanvasCropWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(() =>
        {
            using var rawMaskBitmap = _canvasActionBitmapService.CreateRenderedLayer(request.CanvasActions, request.SourceBitmap.Width, request.SourceBitmap.Height);

            var colorizedBitmap = CreatePreparedWorkflowBitmap(request.SourceBitmap, rawMaskBitmap, request.CanvasUseMode);
            using var ownedColorizedBitmap = colorizedBitmap != null && !ReferenceEquals(colorizedBitmap, request.SourceBitmap)
                ? colorizedBitmap
                : null;

            if (colorizedBitmap == null)
            {
                return null;
            }

            var croppedBitmap = _canvasBitmapService.GetCroppedBitmap(colorizedBitmap, request.BoundingBox, request.BoundingBoxScale, request.BoundingBoxSize);
            using var ownedCroppedBitmap = croppedBitmap != null && !ReferenceEquals(croppedBitmap, colorizedBitmap)
                ? croppedBitmap
                : null;

            var payload = ImagePayloadHelper.CreateFixedPayload(croppedBitmap, request.BoundingBoxSize, request.BoundingBoxSize, PngContentType);
            if (payload == null)
            {
                return null;
            }

            return new CanvasWorkflowNavigationResult(CreateWorkflowNavigationParameters(payload, CreateCroppedWorkflowMaskImageData(rawMaskBitmap, request)));
        });
    }

    public Task<CanvasFlattenWorkflowResult?> ApplyPaintAndMasksAsync(CanvasFlattenWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(() =>
        {
            using var rawMaskBitmap = _canvasActionBitmapService.CreateRenderedLayer(request.CanvasActions, request.SourceBitmap.Width, request.SourceBitmap.Height);

            var mergedBitmap = request.HasMaskActions
                ? _canvasBitmapService.CreateMaskedBitmap(request.SourceBitmap, rawMaskBitmap)
                : request.PreparedSourceBitmap;

            if (mergedBitmap == null)
            {
                return null;
            }

            return new CanvasFlattenWorkflowResult(
                mergedBitmap,
                ReferenceEquals(mergedBitmap, request.PreparedSourceBitmap));
        });
    }

    public async Task<CanvasPatchWorkflowResult?> PatchAsync(CanvasPatchWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _segmentationService.UnloadModel();

        try
        {
            using var mask = await Task.Run(() => _canvasActionBitmapService.CreatePatchMask(request.CanvasActions, request.SourceBitmap.Width, request.SourceBitmap.Height, request.UseLastOnly));
            if (mask == null)
            {
                return null;
            }

            var result = await _patchService.PatchImageAsync(request.SourceBitmap, mask);
            return new CanvasPatchWorkflowResult(result);
        }
        finally
        {
            _patchService.UnloadModel();
        }
    }

    private SKBitmap? CreatePreparedWorkflowBitmap(SKBitmap sourceBitmap, SKBitmap sameSizeMaskBitmap, CanvasUseMode canvasUseMode)
    {
        return canvasUseMode == CanvasUseMode.Inpaint || canvasUseMode == CanvasUseMode.PaintOnly
            ? _canvasBitmapService.CreateMaskedBitmap(sourceBitmap, sameSizeMaskBitmap)
            : sourceBitmap;
    }

    private static bool ShouldIncludeWorkflowMask(CanvasWorkflowRequest request)
    {
        return (request.CanvasUseMode == CanvasUseMode.Inpaint || request.CanvasUseMode == CanvasUseMode.MaskOnly)
            && request.HasMaskActions;
    }

    private static Dictionary<string, object> CreateWorkflowNavigationParameters(ImageTransferPayload payload, string? maskImageDataString = null)
    {
        return new Dictionary<string, object>
        {
            { NavigationParams.ImageWidth, payload.Width },
            { NavigationParams.ImageHeight, payload.Height },
            { NavigationParams.InitImgString, payload.ImageDataString },
            { NavigationParams.InitImgThumbnail, payload.ThumbnailString ?? string.Empty },
            { NavigationParams.MaskImgString, maskImageDataString ?? string.Empty }
        };
    }

    private string CreateWorkflowMaskImageData(SKBitmap rawMaskBitmap, CanvasWorkflowRequest request)
    {
        if (!ShouldIncludeWorkflowMask(request) || !ImagePayloadHelper.HasVisiblePixels(rawMaskBitmap))
        {
            return string.Empty;
        }

        using var blackAndWhiteMaskBitmap = _canvasBitmapService.CreateBlackAndWhiteMask(rawMaskBitmap);
        return ImagePayloadHelper.CreateImageDataString(blackAndWhiteMaskBitmap, PngContentType) ?? string.Empty;
    }

    private string CreateCroppedWorkflowMaskImageData(SKBitmap rawMaskBitmap, CanvasCropWorkflowRequest request)
    {
        if (!ShouldIncludeWorkflowMask(request) || !ImagePayloadHelper.HasVisiblePixels(rawMaskBitmap))
        {
            return string.Empty;
        }

        using var blackAndWhiteMaskBitmap = _canvasBitmapService.CreateBlackAndWhiteMask(rawMaskBitmap);
        if (blackAndWhiteMaskBitmap == null)
        {
            return string.Empty;
        }

        var croppedMask = _canvasBitmapService.GetCroppedBitmap(blackAndWhiteMaskBitmap, request.BoundingBox, request.BoundingBoxScale, request.BoundingBoxSize);
        using var ownedCroppedMask = croppedMask != null && !ReferenceEquals(croppedMask, blackAndWhiteMaskBitmap)
            ? croppedMask
            : null;

        if (!ImagePayloadHelper.HasVisiblePixels(croppedMask))
        {
            return string.Empty;
        }

        return ImagePayloadHelper.CreateImageDataString(croppedMask, PngContentType) ?? string.Empty;
    }
}