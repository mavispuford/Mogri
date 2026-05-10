using Mogri.Enums;
using Mogri.Helpers;
using SkiaSharp;

namespace Mogri.ViewModels;

public partial class CanvasPageViewModel
{
    public override async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.CanvasImageString, out var canvasImageString) &&
            canvasImageString is string byteString)
        {
            var isBoundingBoxReturn = CurrentTool != null && CurrentTool.Type == ToolType.BoundingBox;
            var textHandling = await promptForCanvasResultTextHandlingAsync(isBoundingBoxReturn);

            if (textHandling == CanvasResultTextHandling.Cancel)
            {
                query.Clear();
                return;
            }

            if (isBoundingBoxReturn)
            {
                await BeginStitchingAsync(byteString, textHandling == CanvasResultTextHandling.ResolveText);
            }
            else
            {
                await ApplyCanvasResultAsync(byteString, textHandling == CanvasResultTextHandling.ResolveText);
            }
        }

        if (query.TryGetValue(NavigationParams.AppShareFileUri, out var imageUriFromAppShareParam) &&
            imageUriFromAppShareParam is string imageUri)
        {
            using var stream = await _fileService.GetFileStreamUsingExactUriAsync(imageUri);

            await LoadSourceBitmapUsingStream(stream, Path.GetFileName(imageUri));
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    private async Task ApplyCanvasResultAsync(string byteString, bool resolveTextLayers)
    {
        using var stream = await _imageService.GetStreamFromContentTypeStringAsync(byteString, CancellationToken.None);
        var resultBitmap = SKBitmap.Decode(stream);

        if (resultBitmap == null)
        {
            return;
        }

        var snapshotId = await pushSnapshotAsync("To Canvas", false);
        var oldBitmap = SourceBitmap;

        PreserveZoomOnNextBitmapChange = true;
        SourceBitmap = resultBitmap;
        oldBitmap?.Dispose();

        if (resolveTextLayers)
        {
            TextElements.Clear();
        }

        if (snapshotId != null)
        {
            insertSnapshotMarker(snapshotId, "To Canvas", false);
        }
    }

    private async Task BeginStitchingAsync(string byteString, bool resolveTextLayers)
    {
        var snapshotId = await pushSnapshotAsync("Stitch", false);

        using var stream = await _imageService.GetStreamFromContentTypeStringAsync(byteString, CancellationToken.None);

        var stitchBitmap = SKBitmap.Decode(stream);
        if (stitchBitmap == null)
        {
            return;
        }

        var targetRect = getCanvasResultTargetRect();

        var finalBitmap = _canvasBitmapService.StitchBitmapIntoSource(SourceBitmap, stitchBitmap, BoundingBox, BoundingBoxScale);
        var oldBitmap = SourceBitmap;

        if (snapshotId != null)
        {
            insertSnapshotMarker(snapshotId, "Stitch", false);
        }

        PreserveZoomOnNextBitmapChange = true;
        SourceBitmap = finalBitmap;
        oldBitmap?.Dispose();

        if (resolveTextLayers)
        {
            removeTextElementsIntersecting(targetRect);
        }
    }

    private async Task<CanvasResultTextHandling> promptForCanvasResultTextHandlingAsync(bool isBoundingBoxReturn)
    {
        if (TextElements.Count == 0)
        {
            return CanvasResultTextHandling.KeepEditable;
        }

        var resolveTextOption = isBoundingBoxReturn
            ? "Remove Text/Emoji From Area"
            : "Remove Text/Emoji";

        var keepTextOption = "Keep Text/Emoji for Reuse";

        var action = await _popupService.DisplayActionSheetAsync(
            "Text/Emoji is Present. Options:",
            "Cancel",
            null,
            resolveTextOption,
            keepTextOption);

        if (action == resolveTextOption)
        {
            return CanvasResultTextHandling.ResolveText;
        }

        if (action == keepTextOption)
        {
            return CanvasResultTextHandling.KeepEditable;
        }

        return CanvasResultTextHandling.Cancel;
    }

    private SKRect getCanvasResultTargetRect()
    {
        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null
            || BoundingBoxScale <= 0d
            || BoundingBox.Width <= 0f
            || BoundingBox.Height <= 0f)
        {
            return sourceBitmap?.Info.Rect ?? SKRect.Empty;
        }

        return new SKRect(
            (float)(BoundingBox.Left * BoundingBoxScale),
            (float)(BoundingBox.Top * BoundingBoxScale),
            (float)(BoundingBox.Right * BoundingBoxScale),
            (float)(BoundingBox.Bottom * BoundingBoxScale));
    }

    private void removeTextElementsIntersecting(SKRect targetRect)
    {
        var overlappingTextElements = TextElements
            .Where(textElement => doRectsIntersect(TextElementLayoutHelper.GetAxisAlignedBounds(textElement), targetRect))
            .ToList();

        foreach (var textElement in overlappingTextElements)
        {
            TextElements.Remove(textElement);
        }
    }

    private static bool doRectsIntersect(SKRect left, SKRect right)
    {
        return !left.IsEmpty
            && !right.IsEmpty
            && left.Left < right.Right
            && left.Right > right.Left
            && left.Top < right.Bottom
            && left.Bottom > right.Top;
    }
}