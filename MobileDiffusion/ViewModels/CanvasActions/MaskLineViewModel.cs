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
    public partial float Alpha { get; set; }

    [ObservableProperty]
    public partial float BrushSize { get; set; }
    
    [ObservableProperty]
    public partial Color Color { get; set; }

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

    public override CanvasActionViewModel Clone()
    {
        return new MaskLineViewModel
        {
            CanvasActionType = CanvasActionType,
            Order = Order,
            Alpha = Alpha,
            BrushSize = BrushSize,
            Color = Color,
            MaskEffect = MaskEffect,
            TouchScale = TouchScale,
            Path = new List<SKPoint>(Path)
        };
    }

    partial void OnAlphaChanged(float value)
    {
        updateShader();
    }

    partial void OnColorChanged(Color value)
    {
        updateShader();
    }

    private void updateShader()
    {
        if (Color == null || Alpha < 0 || Alpha > 1)
        {
            return;
        }

        _paintColor = new SKColor(
                Color.GetByteRed(),
                Color.GetByteGreen(),
                Color.GetByteBlue(),
                Convert.ToByte((int)Math.Max(1, Alpha * 255)));

        _bitmapShader = MaskHelper.CreateMaskBitmapShaderLines(_paintColor);
    }
}
