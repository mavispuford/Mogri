using Mogri.Enums;
using SkiaSharp;

namespace Mogri.ViewModels;

public partial class CanvasPageViewModel
{
    private SKBitmap GenerateRenderedLayer(int width, int height)
    {
        var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.Transparent);

            // Render all actions with isSaving=true to ensure High Fidelity (Noise/Color)
            // instead of UI Fallbacks (Hatch Patterns).
            var info = new SKImageInfo(width, height);

            if (CanvasActions != null)
            {
                foreach (var action in CanvasActions)
                {
                    action.Execute(canvas, info, true);
                }
            }
        }
        return bmp;
    }

    private SKBitmap? GenerateMask(bool useLastOnly)
    {
        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null) return null;

        var mask = new SKBitmap(sourceBitmap.Width, sourceBitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(mask);

        // Background is black (keep original)
        canvas.Clear(SKColors.Black);

        if (CanvasActions != null)
        {
            // Filter actions
            var actionsToRender = useLastOnly
                ? CanvasActions.Where(x => x == CanvasActions.LastOrDefault()).ToList()
                : CanvasActions.ToList();

            // Draw masks (White = Inpaint)
            foreach (var action in actionsToRender)
            {
                if (action is MaskLineViewModel line)
                {
                    using var paint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = line.BrushSize,
                        StrokeCap = SKStrokeCap.Round,
                        StrokeJoin = SKStrokeJoin.Round,
                        IsAntialias = true
                    };

                    if (line.MaskEffect == MaskEffect.Paint)
                    {
                        paint.BlendMode = SKBlendMode.SrcOver;
                        paint.Color = SKColors.White;
                    }
                    else // Erase
                    {
                        paint.BlendMode = SKBlendMode.Src;
                        paint.Color = SKColors.Black;
                    }

                    if (line.Path != null && line.Path.Count > 0)
                    {
                        using var path = new SKPath();
                        path.MoveTo(line.Path[0]);
                        for (var i = 1; i < line.Path.Count; i++)
                        {
                            path.ConicTo(line.Path[i - 1], line.Path[i], .5f);
                        }
                        canvas.DrawPath(path, paint);
                    }
                }
                else if (action is SegmentationMaskViewModel seg && seg.Bitmap != null)
                {
                    using var paint = new SKPaint();
                    // Create filter to make non-transparent pixels white
                    paint.ColorFilter = SKColorFilter.CreateBlendMode(SKColors.White, SKBlendMode.SrcIn);

                    canvas.DrawBitmap(seg.Bitmap, new SKRect(0, 0, mask.Width, mask.Height), paint);
                }
            }
        }

        return mask;
    }
}