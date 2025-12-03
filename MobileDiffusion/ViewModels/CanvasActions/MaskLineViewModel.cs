using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Enums;
using MobileDiffusion.Helpers;
using SkiaSharp;

namespace MobileDiffusion.ViewModels;

public partial class MaskLineViewModel : CanvasActionViewModel
{
    private SKShader _bitmapShader;
    private SKColor _paintColor;

    [ObservableProperty]
    private float _alpha;

    [ObservableProperty]
    private float _brushSize;
    
    [ObservableProperty]
    private Color _color;

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

        for (var i = 1; i < points.Count; i++)
        {
            path.ConicTo(points[i - 1], points[i], .5f);
        }

        if (MaskEffect == MaskEffect.Paint &&
            !isSaving && Alpha <= .1f && 
            _bitmapShader != null)
        {
            paint.Shader = _bitmapShader;
            paint.Color = paint.Color.WithAlpha(255);
        }
        else
        {
            paint.Shader = null;
        }

        canvas.DrawPath(path, paint);
    }

    partial void OnColorChanged(Color value)
    {
        if (value != null)
        {
            _paintColor = new SKColor(
                value.GetByteRed(),
                value.GetByteGreen(),
                value.GetByteBlue(),
                Convert.ToByte((int)Math.Max(1, Alpha * 255)));

            _bitmapShader = MaskHelper.CreateMaskBitmapShaderLines(_paintColor);
        }
    }
}
