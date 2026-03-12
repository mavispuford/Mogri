using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using Mogri.Enums;
using Mogri.Helpers;
using SkiaSharp;

namespace Mogri.ViewModels;

public partial class MaskLineViewModel : PaintActionViewModel
{
    [ObservableProperty]
    public partial float BrushSize { get; set; }

    public float TouchScale { get; set; } = 1f;

    public List<SKPoint> Path { get; set; } = new();
    public MaskEffect MaskEffect { get; set; }

    public override void Execute(SKCanvas canvas, SKImageInfo imageInfo, bool isSaving)
    {
        using var paint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = BrushSize,
            StrokeCap = SKStrokeCap.Round,
            StrokeMiter = 0,
            StrokeJoin = SKStrokeJoin.Round,
        };

        if (MaskEffect == MaskEffect.Paint)
        {
            paint.BlendMode = SKBlendMode.SrcOver;

            paint.Color = _paintColor;
        }
        else
        {
            paint.BlendMode = SKBlendMode.Src;

            paint.Color = SKColors.Transparent;
        }

        var points = Path;
        if (points == null || points.Count == 0)
        {
            return;
        }

        using var path = new SKPath();
        path.MoveTo(points[0]);

        if (points.Count > 2)
        {
            // The Midpoint Algorithm
            for (var i = 1; i < points.Count - 1; i++)
            {
                var current = points[i];
                var next = points[i + 1];

                // Calculate the midpoint between the current and next point
                var midPoint = new SKPoint((current.X + next.X) / 2, (current.Y + next.Y) / 2);

                // QuadTo uses the 'current' point as the control point (the "pull")
                // and the 'midPoint' as the actual destination.
                path.QuadTo(current, midPoint);
            }

            // Connect to the very last point to finish the line
            path.LineTo(points[^1]);
        }
        else if (points.Count == 2)
        {
            path.LineTo(points[1]);
        }
        else if (points.Count == 1)
        {
            // Draw a tiny line/dot so a single tap is visible
            path.LineTo(points[0].X, points[0].Y);
        }

        if (MaskEffect == MaskEffect.Paint)
        {
            // PRIORITY 1: Visual Fallback for Low Alpha (Only when NOT saving)
            // If the alpha is very low, we show the hatch pattern so the user can see where they are drawing.
            // But if we are saving (Exporting), we skip this validation so the actual noise/color is rendered.
            if (!isSaving && Alpha <= 0.1f && _bitmapShader != null)
            {
                paint.Shader = _bitmapShader;
                paint.Color = SKColors.White; // Full opacity for the hatch pattern lines
            }
            // PRIORITY 2: Noise Shader (If enabled)
            // Used for saving OR if alpha is high enough to be visible.
            else if (Noise > 0 && _noiseShader != null)
            {
                // Use the cached Noise shader (created in PaintActionViewModel.UpdateShaders)
                paint.Shader = _noiseShader;
            }
            else
            {
                paint.Shader = null;
            }
        }
        else
        {
            paint.Shader = null;
        }

        canvas.DrawPath(path, paint);
    }

    public override CanvasActionViewModel Clone()
    {
        return new MaskLineViewModel
        {
            CanvasActionType = CanvasActionType,
            Order = Order,
            Alpha = Alpha,
            BrushSize = BrushSize,
            Color = Color,
            Noise = Noise,
            MaskEffect = MaskEffect,
            TouchScale = TouchScale,
            Path = new List<SKPoint>(Path)
        };
    }
}
