using Mogri.Enums;
using Mogri.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Mogri.Views;

/// <summary>
/// Canvas page partial that handles non-text touch routing for drawing, segmentation, eyedropper, and bounding-box interactions.
/// </summary>
public partial class CanvasPage
{
    // Active canvas drawing state.
    private MaskLineViewModel? _currentLine;
    private MaskLineViewModel? _segmentationLine;

    private void OnTouchTemporarySurface(object? sender, SKTouchEventArgs e)
    {
        HideSliders();

        if (e.Location is SKPoint location && CurrentTool != null)
        {
            float scale = 1f;
            var viewWidth = TemporaryCanvasView.CanvasSize.Width > 0 ? TemporaryCanvasView.CanvasSize.Width : MaskCanvasView.CanvasSize.Width;

            if (Bitmap != null && viewWidth > 0)
            {
                scale = (float)Bitmap.Width / viewWidth;
            }

            var imageLocation = new SKPoint(location.X * scale, location.Y * scale);

            if (CurrentTool.Type == ToolType.Text)
            {
                handleTextToolTouch(e, location, imageLocation);
                TemporaryCanvasView.InvalidateSurface();
                e.Handled = true;
                return;
            }

            // InContact == Finger currently touching down
            if (e.InContact)
            {
                switch (CurrentTool.Type)
                {
                    case ToolType.BoundingBox:
                        if (ShowBoundingBox &&
                            BoundingBox.Width > 0 &&
                            BoundingBox.Height > 0 &&
                            BoundingBox.Contains(location))
                        {
                            var offsetX = -(BoundingBox.Width / 2);
                            var offsetY = -(BoundingBox.Height / 2);

                            if (location.X + offsetX < 0)
                            {
                                offsetX = -location.X;
                            }
                            else if (location.X + offsetX + BoundingBox.Width > MaskCanvasView.Width)
                            {
                                offsetX = (float)MaskCanvasView.Width - location.X - BoundingBox.Width;
                            }

                            if (location.Y + offsetY < 0)
                            {
                                offsetY = -location.Y;
                            }
                            else if (location.Y + offsetY + BoundingBox.Height > MaskCanvasView.Height)
                            {
                                offsetY = (float)MaskCanvasView.Height - location.Y - BoundingBox.Height;
                            }

                            location.Offset(offsetX, offsetY);

                            BoundingBox = SKRect.Create(location, BoundingBox.Size);
                        }

                        break;
                    case ToolType.Eyedropper:
                        if (Bitmap != null)
                        {
                            var x = (int)((location.X / TemporaryCanvasView.Width) * Bitmap.Width);
                            var y = (int)((location.Y / TemporaryCanvasView.Height) * Bitmap.Height);

                            if (x >= 0 && x < Bitmap.Width && y >= 0 && y < Bitmap.Height)
                            {
                                var pixelColor = Bitmap.GetPixel(x, y);
                                CurrentColor = pixelColor.ToMauiColor();
                            }
                        }
                        break;
                    case ToolType.PaintBrush:
                    case ToolType.Eraser:
                        ShowMaskLayer = true;

                        if (_currentLine == null)
                        {
                            _currentLine = new()
                            {
                                CanvasActionType = CanvasActionType.Mask,
                                Alpha = (float)CurrentAlpha,
                                BrushSize = (float)CurrentBrushSize * scale,
                                TouchScale = scale,
                                Color = CurrentColor,
                                Noise = CurrentNoise,
                                MaskEffect = CurrentTool?.Effect ?? MaskEffect.Paint
                            };
                        }

                        _currentLine.Path.Add(imageLocation);

                        if (_currentLine.MaskEffect == MaskEffect.Erase)
                        {
                            MaskCanvasView.InvalidateSurface();
                        }

                        break;
                    case ToolType.MagicWand:
                        ShowMaskLayer = true;

                        _segmentationLine ??= new()
                        {
                            CanvasActionType = CanvasActionType.Mask,
                            Alpha = .75f,
                            BrushSize = 10f * scale,
                            TouchScale = scale,
                            Color = Colors.White,
                            Noise = CurrentNoise,
                            MaskEffect = MaskEffect.Paint
                        };

                        _segmentationLine.Path.Add(imageLocation);

                        break;

                }
            }
            else
            {
                // Touch/click has been released

                if (_currentLine != null)
                {
                    _currentLine.Order = GetNextCanvasOrder();
                    CanvasActions?.Add(_currentLine);
                    _currentLine = null;

                    MaskCanvasView.InvalidateSurface();
                }

                switch (CurrentTool.Type)
                {
                    case ToolType.MagicWand:
                        if (_segmentationLine != null &&
                            _segmentationLine.Path.Count > 1)
                        {
                            var left = _segmentationLine.Path.Min(p => p.X);
                            var right = _segmentationLine.Path.Max(p => p.X);
                            var top = _segmentationLine.Path.Min(p => p.Y);
                            var bottom = _segmentationLine.Path.Max(p => p.Y);

                            var bounds = new SKRect(left, top, right, bottom);

                            if (bounds.Size.Width < (10 * scale) &&
                                bounds.Size.Height < (10 * scale))
                            {
                                DoSegmentationCommand?.Execute([imageLocation]);
                            }
                            else
                            {
                                var topLeft = new SKPoint(left, top);
                                var bottomRight = new SKPoint(right, bottom);

                                DoSegmentationCommand?.Execute([topLeft, bottomRight]);
                            }
                        }
                        else
                        {
                            DoSegmentationCommand?.Execute([imageLocation]);
                        }

                        _segmentationLine = null;
                        break;
                }
            }
        }

        TemporaryCanvasView.InvalidateSurface();

        e.Handled = true;
    }

    private int GetNextCanvasOrder()
    {
        var nextCanvasActionOrder = CanvasActions?.Count > 0
            ? CanvasActions.Max(canvasAction => canvasAction.Order) + 1
            : 0;
        var nextTextOrder = TextElements?.Count > 0
            ? checked((int)(TextElements.Max(textElement => textElement.Order) + 1))
            : 0;

        return Math.Max(nextCanvasActionOrder, nextTextOrder);
    }
}