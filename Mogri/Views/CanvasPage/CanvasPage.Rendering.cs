using CommunityToolkit.Mvvm.Input;
using Mogri.Enums;
using Mogri.Helpers;
using Mogri.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Mogri.Views;

/// <summary>
/// Canvas page partial that paints the canvas surfaces and prepares rendered output for save workflows.
/// </summary>
public partial class CanvasPage
{
    // Page-level view state.
    private bool _isSaving;

    private void OnPaintSourceImageSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (Bitmap != null)
        {
            canvas.DrawBitmap(Bitmap, Bitmap.Info.Rect, e.Info.Rect);
        }
    }

    private void OnPaintMaskSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (Bitmap == null)
        {
            return;
        }

        // Calculate scale to transform Image Coords -> View Coords
        // e.Info.Width is ViewPixels. Bitmap.Width is ImagePixels.
        // Scale = View / Image.
        var scale = (float)e.Info.Width / Bitmap.Width;

        if (CanvasActions != null)
        {
            foreach (var canvasAction in CanvasActions
                .Where(canvasAction => canvasAction.CanvasActionType == CanvasActionType.Mask)
                .OrderBy(canvasAction => canvasAction.Order))
            {
                canvas.Save();
                canvas.Scale(scale);

                var virtualInfo = new SKImageInfo(Bitmap.Width, Bitmap.Height, e.Info.ColorType, e.Info.AlphaType);
                canvasAction.Execute(canvas, virtualInfo, _isSaving);

                canvas.Restore();
            }
        }

        if (_currentLine != null && _currentLine.MaskEffect == MaskEffect.Erase)
        {
            canvas.Save();
            canvas.Scale(scale);
            _currentLine.Execute(canvas, e.Info, _isSaving);
            canvas.Restore();
        }
    }

    private void OnPaintTextSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (Bitmap == null || TextElements == null)
        {
            return;
        }

        var scale = (float)e.Info.Width / Bitmap.Width;

        foreach (var textElement in TextElements.OrderBy(textElement => textElement.Order))
        {
            CanvasTextRenderer.DrawTextElement(canvas, textElement, scale);
        }
    }

    private void OnPaintTemporarySurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        float scale = 1f;
        if (Bitmap != null)
        {
            scale = (float)e.Info.Width / Bitmap.Width;
        }

        if (_currentLine != null && _currentLine.MaskEffect == MaskEffect.Paint)
        {
            canvas.Save();
            canvas.Scale(scale);
            _currentLine.Execute(canvas, e.Info, _isSaving);
            canvas.Restore();
        }

        if (_segmentationLine != null)
        {
            canvas.Save();
            canvas.Scale(scale);
            _segmentationLine.Execute(canvas, e.Info, _isSaving);
            canvas.Restore();
        }

        drawSelectedTextOutline(canvas, scale);

        if (ShowBoundingBox)
        {
            canvas.DrawRect(BoundingBox,
            new SKPaint()
            {
                Color = SKColors.Black.WithAlpha((byte)15),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 6,
            });

            var boxPaint = new SKPaint()
            {
                Color = SKColors.White,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
            };

            canvas.DrawRect(BoundingBox, boxPaint);
        }
    }

    private void OnPaintSegmentationImageSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (SegmentationBitmap != null)
        {
            canvas.DrawBitmap(SegmentationBitmap, SegmentationBitmap.Info.Rect, e.Info.Rect);
        }
    }

    private void drawSelectedTextOutline(SKCanvas canvas, float canvasScale)
    {
        var selectedTextElement = _textInteraction.SelectedTextElement;

        if (selectedTextElement == null
            || CurrentTool?.Type != ToolType.Text
            || TextElements == null
            || !TextElements.Contains(selectedTextElement)
            || string.IsNullOrWhiteSpace(selectedTextElement.Text))
        {
            return;
        }

        CanvasTextRenderer.DrawSelectionOutline(
            canvas,
            selectedTextElement,
            canvasScale,
            TextSelectionPadding,
            TextSelectionCornerRadius,
            TextSelectionShadowStroke,
            TextSelectionStroke);
    }

    private async Task PrepareForSaving(IAsyncRelayCommand? callbackCommand)
    {
        if (callbackCommand == null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            var result = new CanvasCaptureResult();

            if (Bitmap != null && TextElements is { Count: > 0 })
            {
                result.PreparedSourceBitmap = CanvasTextRenderer.PrepareSourceBitmapWithText(Bitmap, TextElements);
            }

            await callbackCommand.ExecuteAsync(result);
        }
        finally
        {
            _isSaving = false;
        }
    }
}