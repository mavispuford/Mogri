using SkiaSharp;

namespace Mogri.Views;

/// <summary>
/// Canvas page partial that manages canvas sizing, bounding-box layout, and size-driven invalidation behavior.
/// </summary>
public partial class CanvasPage
{
    // Page-level layout state.
    private bool _hasCreatedBoundingBox;

    private void TemporaryCanvasView_SizeChanged(object? sender, EventArgs e)
    {
        if (TemporaryCanvasView.Width != -1 &&
            TemporaryCanvasView.Height != -1)
        {
            UpdateBoundingBox(true, true);
        }
    }

    private void UpdateBoundingBox(bool sizeChanged, bool resetPosition = false)
    {
        var rectSize = (float)(BoundingBoxSize / BoundingBoxScale);

        if ((!_hasCreatedBoundingBox || resetPosition) &&
            TemporaryCanvasView.Width != -1 &&
            TemporaryCanvasView.Height != -1)
        {
            BoundingBox = new SKRect(
                (float)(TemporaryCanvasView.Width / 2) - (rectSize / 2),
                (float)(TemporaryCanvasView.Height / 2) - (rectSize / 2),
                (float)(TemporaryCanvasView.Width / 2) + (rectSize / 2),
                (float)(TemporaryCanvasView.Height / 2) + (rectSize / 2));

            _hasCreatedBoundingBox = true;
        }
        else if (sizeChanged)
        {
            BoundingBox = new SKRect(
                BoundingBox.MidX - (rectSize / 2),
                BoundingBox.MidY - (rectSize / 2),
                BoundingBox.MidX + (rectSize / 2),
                BoundingBox.MidY + (rectSize / 2));
        }

        TemporaryCanvasView.InvalidateSurface();
    }

    private void UpdateCanvasSizes()
    {
        if (Bitmap == null)
        {
            return;
        }

        var scale = Math.Min((float)MaskGrid.Width / Bitmap.Width, (float)MaskGrid.Height / Bitmap.Height);
        var width = scale * Bitmap.Width;
        var height = scale * Bitmap.Height;

        SourceImageCanvasView.WidthRequest = width;
        SourceImageCanvasView.HeightRequest = height;

        TextCanvasView.WidthRequest = width;
        TextCanvasView.HeightRequest = height;

        MaskCanvasView.WidthRequest = width;
        MaskCanvasView.HeightRequest = height;

        SegmentationMaskCanvasView.WidthRequest = width;
        SegmentationMaskCanvasView.HeightRequest = height;

        TemporaryCanvasView.WidthRequest = width;
        TemporaryCanvasView.HeightRequest = height;

        // Force a measure on both canvas views because setting width/height request doesn't seem to be enough.
        SourceImageCanvasView.Measure(width, height);
        SourceImageCanvasView.InvalidateSurface();
        TextCanvasView.Measure(width, height);
        TextCanvasView.InvalidateSurface();
        MaskCanvasView.Measure(width, height);
        MaskCanvasView.InvalidateSurface();
        SegmentationMaskCanvasView.Measure(width, height);
        SegmentationMaskCanvasView.InvalidateSurface();
        TemporaryCanvasView.Measure(width, height);
        TemporaryCanvasView.InvalidateSurface();

        BoundingBoxScale = Bitmap.Width / width;
    }

    private void MaskGrid_SizeChanged(object? sender, EventArgs e)
    {
        UpdateCanvasSizes();
    }
}