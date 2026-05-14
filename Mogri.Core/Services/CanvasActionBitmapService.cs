using Mogri.Interfaces.Services;
using SkiaSharp;

namespace Mogri.Services;

/// <summary>
/// Renders canvas-action-driven workflow layers and patch masks without UI dependencies.
/// </summary>
public sealed class CanvasActionBitmapService : ICanvasActionBitmapService
{
    public SKBitmap CreateRenderedLayer(IEnumerable<ICanvasRenderAction>? actions, int width, int height)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);

            // Render all actions with isSaving=true to ensure high-fidelity export output.
            var info = new SKImageInfo(width, height);

            foreach (var action in actions ?? Enumerable.Empty<ICanvasRenderAction>())
            {
                action.Execute(canvas, info, true);
            }
        }

        return bitmap;
    }

    public SKBitmap CreatePatchMask(IEnumerable<ICanvasRenderAction>? actions, int width, int height, bool useLastOnly)
    {
        var mask = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var canvas = new SKCanvas(mask);

        // Background is black (keep original)
        canvas.Clear(SKColors.Black);

        var actionList = actions?.ToList() ?? new List<ICanvasRenderAction>();
        var actionsToRender = useLastOnly
            ? actionList.TakeLast(1)
            : actionList;

        foreach (var action in actionsToRender)
        {
            switch (action)
            {
                case ICanvasMaskStrokeAction strokeAction:
                    DrawMaskStroke(canvas, strokeAction);
                    break;
                case ICanvasBitmapMaskAction bitmapMaskAction when bitmapMaskAction.Bitmap != null:
                    DrawBitmapMask(canvas, bitmapMaskAction.Bitmap, width, height);
                    break;
            }
        }

        return mask;
    }

    private static void DrawMaskStroke(SKCanvas canvas, ICanvasMaskStrokeAction strokeAction)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeAction.BrushSize,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };

        if (strokeAction.AddsToMask)
        {
            paint.BlendMode = SKBlendMode.SrcOver;
            paint.Color = SKColors.White;
        }
        else
        {
            paint.BlendMode = SKBlendMode.Src;
            paint.Color = SKColors.Black;
        }

        if (strokeAction.Points.Count == 0)
        {
            return;
        }

        using var path = new SKPath();
        path.MoveTo(strokeAction.Points[0]);

        for (var index = 1; index < strokeAction.Points.Count; index++)
        {
            path.ConicTo(strokeAction.Points[index - 1], strokeAction.Points[index], .5f);
        }

        canvas.DrawPath(path, paint);
    }

    private static void DrawBitmapMask(SKCanvas canvas, SKBitmap bitmap, int width, int height)
    {
        using var paint = new SKPaint();
        paint.ColorFilter = SKColorFilter.CreateBlendMode(SKColors.White, SKBlendMode.SrcIn);

        canvas.DrawBitmap(bitmap, new SKRect(0, 0, width, height), paint);
    }
}