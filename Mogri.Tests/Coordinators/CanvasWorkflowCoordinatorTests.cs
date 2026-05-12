using Moq;
using Mogri.Coordinators;
using Mogri.Enums;
using Mogri.Interfaces.Services;
using Mogri.Models;
using SkiaSharp;
using Xunit;

namespace Mogri.Tests.Coordinators;

public class CanvasWorkflowCoordinatorTests
{
    [Fact]
    public async Task SaveImageAsync_WithBitmap_WritesPngToExternalStorage()
    {
        // Arrange
        var actionBitmapService = new Mock<ICanvasActionBitmapService>();
        var bitmapService = new Mock<ICanvasBitmapService>();
        var fileService = new Mock<IFileService>();
        var patchService = new Mock<IPatchService>();
        var segmentationService = new Mock<ISegmentationService>();
        using var sourceBitmap = CreateBitmap(SKColors.CadetBlue);
        using var capturedStream = new MemoryStream();

        fileService
            .Setup(service => service.WriteImageFileToExternalStorageAsync(It.IsAny<string>(), It.IsAny<Stream>(), false))
            .Returns<string, Stream, bool>(async (_, stream, _) =>
            {
                await stream.CopyToAsync(capturedStream);
            });

        var coordinator = CreateCoordinator(actionBitmapService, bitmapService, fileService, patchService, segmentationService);

        // Act
        var fileName = await coordinator.SaveImageAsync(sourceBitmap);

        // Assert
        Assert.StartsWith("CanvasImage-", fileName, StringComparison.Ordinal);
        Assert.EndsWith(".png", fileName, StringComparison.Ordinal);
        Assert.True(capturedStream.Length > 0);
        fileService.Verify(
            service => service.WriteImageFileToExternalStorageAsync(It.IsAny<string>(), It.IsAny<Stream>(), false),
            Times.Once);
    }

    [Fact]
    public async Task CreateImageToImageNavigationAsync_InpaintWithVisibleMask_IncludesMaskPayload()
    {
        // Arrange
        var actionBitmapService = new Mock<ICanvasActionBitmapService>();
        var bitmapService = new Mock<ICanvasBitmapService>();
        var fileService = new Mock<IFileService>();
        var patchService = new Mock<IPatchService>();
        var segmentationService = new Mock<ISegmentationService>();
        using var sourceBitmap = CreateBitmap(SKColors.White);

        actionBitmapService
            .Setup(service => service.CreateRenderedLayer(It.IsAny<IEnumerable<ICanvasRenderAction>>(), 2, 2))
            .Returns(CreateMaskBitmap());
        bitmapService
            .Setup(service => service.CreateMaskedBitmap(sourceBitmap, It.IsAny<SKBitmap>()))
            .Returns(CreateBitmap(SKColors.Black));
        bitmapService
            .Setup(service => service.CreateBlackAndWhiteMask(It.IsAny<SKBitmap>()))
            .Returns(CreateMaskBitmap());

        var coordinator = CreateCoordinator(actionBitmapService, bitmapService, fileService, patchService, segmentationService);

        var request = new CanvasWorkflowRequest
        {
            SourceBitmap = sourceBitmap,
            CanvasActions = Array.Empty<ICanvasRenderAction>(),
            CanvasUseMode = CanvasUseMode.Inpaint,
            HasMaskActions = true
        };

        // Act
        var result = await coordinator.CreateImageToImageNavigationAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(string.Empty, Assert.IsType<string>(result!.Parameters[NavigationParams.MaskImgString]));
        Assert.NotEqual(string.Empty, Assert.IsType<string>(result.Parameters[NavigationParams.InitImgString]));
    }

    [Fact]
    public async Task CreateImageToImageNavigationAsync_ImageOnly_LeavesMaskPayloadEmpty()
    {
        // Arrange
        var actionBitmapService = new Mock<ICanvasActionBitmapService>();
        var bitmapService = new Mock<ICanvasBitmapService>();
        var fileService = new Mock<IFileService>();
        var patchService = new Mock<IPatchService>();
        var segmentationService = new Mock<ISegmentationService>();
        using var sourceBitmap = CreateBitmap(SKColors.White);

        actionBitmapService
            .Setup(service => service.CreateRenderedLayer(It.IsAny<IEnumerable<ICanvasRenderAction>>(), 2, 2))
            .Returns(CreateMaskBitmap());

        var coordinator = CreateCoordinator(actionBitmapService, bitmapService, fileService, patchService, segmentationService);

        var request = new CanvasWorkflowRequest
        {
            SourceBitmap = sourceBitmap,
            CanvasActions = Array.Empty<ICanvasRenderAction>(),
            CanvasUseMode = CanvasUseMode.ImageOnly,
            HasMaskActions = false
        };

        // Act
        var result = await coordinator.CreateImageToImageNavigationAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, Assert.IsType<string>(result!.Parameters[NavigationParams.MaskImgString]));
        bitmapService.Verify(service => service.CreateBlackAndWhiteMask(It.IsAny<SKBitmap>()), Times.Never);
    }

    [Fact]
    public async Task CreateCropNavigationAsync_WithCropRequest_UsesFixedBoundingBoxSize()
    {
        // Arrange
        var actionBitmapService = new Mock<ICanvasActionBitmapService>();
        var bitmapService = new Mock<ICanvasBitmapService>();
        var fileService = new Mock<IFileService>();
        var patchService = new Mock<IPatchService>();
        var segmentationService = new Mock<ISegmentationService>();
        using var sourceBitmap = CreateBitmap(SKColors.White);

        actionBitmapService
            .Setup(service => service.CreateRenderedLayer(It.IsAny<IEnumerable<ICanvasRenderAction>>(), 2, 2))
            .Returns(CreateMaskBitmap());
        bitmapService
            .Setup(service => service.CreateMaskedBitmap(sourceBitmap, It.IsAny<SKBitmap>()))
            .Returns(CreateBitmap(SKColors.Orange));
        bitmapService
            .Setup(service => service.GetCroppedBitmap(It.IsAny<SKBitmap>(), It.IsAny<SKRect>(), 2d, 512f))
            .Returns(CreateBitmap(SKColors.Green));
        bitmapService
            .Setup(service => service.CreateBlackAndWhiteMask(It.IsAny<SKBitmap>()))
            .Returns(CreateMaskBitmap());

        var coordinator = CreateCoordinator(actionBitmapService, bitmapService, fileService, patchService, segmentationService);

        var request = new CanvasCropWorkflowRequest
        {
            SourceBitmap = sourceBitmap,
            CanvasActions = Array.Empty<ICanvasRenderAction>(),
            CanvasUseMode = CanvasUseMode.Inpaint,
            HasMaskActions = true,
            BoundingBox = new SKRect(0, 0, 2, 2),
            BoundingBoxScale = 2d,
            BoundingBoxSize = 512f
        };

        // Act
        var result = await coordinator.CreateCropNavigationAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(512d, Assert.IsType<double>(result!.Parameters[NavigationParams.ImageWidth]));
        Assert.Equal(512d, Assert.IsType<double>(result.Parameters[NavigationParams.ImageHeight]));
        Assert.NotEqual(string.Empty, Assert.IsType<string>(result.Parameters[NavigationParams.MaskImgString]));
    }

    [Fact]
    public async Task ApplyPaintAndMasksAsync_WithoutMaskActions_ReturnsPreparedBitmapOwnership()
    {
        // Arrange
        var actionBitmapService = new Mock<ICanvasActionBitmapService>();
        var bitmapService = new Mock<ICanvasBitmapService>();
        var fileService = new Mock<IFileService>();
        var patchService = new Mock<IPatchService>();
        var segmentationService = new Mock<ISegmentationService>();
        using var sourceBitmap = CreateBitmap(SKColors.White);
        using var preparedSourceBitmap = CreateBitmap(SKColors.Blue);

        actionBitmapService
            .Setup(service => service.CreateRenderedLayer(It.IsAny<IEnumerable<ICanvasRenderAction>>(), 2, 2))
            .Returns(CreateMaskBitmap());

        var coordinator = CreateCoordinator(actionBitmapService, bitmapService, fileService, patchService, segmentationService);

        var request = new CanvasFlattenWorkflowRequest
        {
            SourceBitmap = sourceBitmap,
            PreparedSourceBitmap = preparedSourceBitmap,
            CanvasActions = Array.Empty<ICanvasRenderAction>(),
            HasMaskActions = false
        };

        // Act
        var result = await coordinator.ApplyPaintAndMasksAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Same(preparedSourceBitmap, result!.MergedBitmap);
        Assert.True(result.TransfersPreparedSourceBitmapOwnership);
        bitmapService.Verify(service => service.CreateMaskedBitmap(It.IsAny<SKBitmap>(), It.IsAny<SKBitmap>()), Times.Never);
    }

    [Fact]
    public async Task PatchAsync_UseLastOnly_PatchesImageAndUnloadsServices()
    {
        // Arrange
        var actionBitmapService = new Mock<ICanvasActionBitmapService>();
        var bitmapService = new Mock<ICanvasBitmapService>();
        var fileService = new Mock<IFileService>();
        var patchService = new Mock<IPatchService>();
        var segmentationService = new Mock<ISegmentationService>();
        using var sourceBitmap = CreateBitmap(SKColors.White);
        using var patchedBitmap = CreateBitmap(SKColors.Red);

        actionBitmapService
            .Setup(service => service.CreatePatchMask(It.IsAny<IEnumerable<ICanvasRenderAction>>(), 2, 2, true))
            .Returns(CreateMaskBitmap());
        patchService
            .Setup(service => service.PatchImageAsync(sourceBitmap, It.IsAny<SKBitmap>()))
            .ReturnsAsync(patchedBitmap);

        var coordinator = CreateCoordinator(actionBitmapService, bitmapService, fileService, patchService, segmentationService);

        var request = new CanvasPatchWorkflowRequest
        {
            SourceBitmap = sourceBitmap,
            CanvasActions = Array.Empty<ICanvasRenderAction>(),
            UseLastOnly = true
        };

        // Act
        var result = await coordinator.PatchAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Same(patchedBitmap, result!.PatchedBitmap);
        segmentationService.Verify(service => service.UnloadModel(), Times.Once);
        patchService.Verify(service => service.UnloadModel(), Times.Once);
        actionBitmapService.Verify(service => service.CreatePatchMask(It.IsAny<IEnumerable<ICanvasRenderAction>>(), 2, 2, true), Times.Once);
    }

    private static CanvasWorkflowCoordinator CreateCoordinator(
        Mock<ICanvasActionBitmapService> actionBitmapService,
        Mock<ICanvasBitmapService> bitmapService,
        Mock<IFileService> fileService,
        Mock<IPatchService> patchService,
        Mock<ISegmentationService> segmentationService)
    {
        return new CanvasWorkflowCoordinator(
            actionBitmapService.Object,
            bitmapService.Object,
            fileService.Object,
            patchService.Object,
            segmentationService.Object);
    }

    private static SKBitmap CreateBitmap(SKColor color)
    {
        var bitmap = new SKBitmap(2, 2);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        return bitmap;
    }

    private static SKBitmap CreateMaskBitmap()
    {
        var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.Transparent);
        bitmap.SetPixel(0, 0, SKColors.White);
        return bitmap;
    }
}