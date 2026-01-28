using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Enums;
using MobileDiffusion.Helpers;
using SkiaSharp;

namespace MobileDiffusion.ViewModels;

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

        using var path = new SKPath();
        path.MoveTo(points[0]);

        // Create the path
        if (points.Count > 1)
        {
            for (var i = 1; i < points.Count; i++)
            {
                path.ConicTo(points[i - 1], points[i], .5f);
            }
        }
        else if (points.Count == 1)
        {
            // This will draw a single dot if there is only one point
            path.ConicTo(points[0], points[0], .5f);
        }
        
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
