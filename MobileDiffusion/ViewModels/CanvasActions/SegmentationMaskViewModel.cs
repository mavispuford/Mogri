using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Helpers;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace MobileDiffusion.ViewModels;

public partial class SegmentationMaskViewModel : CanvasActionViewModel
{
    private SKShader _bitmapOutlineShader;

    [ObservableProperty]
    public partial SKBitmap Bitmap { get; set; }

    [ObservableProperty]
    public partial Color Color { get; set; }

    public override void Execute(SKCanvas canvas, SKImageInfo imageInfo, bool isSaving)
    {
        if (Bitmap != null)
        {            
            canvas.DrawBitmap(Bitmap, Bitmap.Info.Rect, imageInfo.Rect);

            //if (!isSaving && Color.Alpha <= .1f && _bitmapOutlineShader != null)
            //{
            //    using var paint = new SKPaint
            //    {
            //        FilterQuality = SKFilterQuality.None,
            //        IsAntialias = false,
            //        Style = SKPaintStyle.Fill,
            //        Color = Color.ToSKColor().WithAlpha(255),
            //        BlendMode = SKBlendMode.Modulate
            //    };

            //    paint.Shader = _bitmapOutlineShader;

            //    canvas.DrawRect(0, 0, Bitmap.Width, Bitmap.Height, paint);
            //}
        }
    }

    partial void OnColorChanged(Color value)
    {
        if (value != null)
        {
            _bitmapOutlineShader = MaskHelper.CreateMaskBitmapShaderLines(value.ToSKColor());
        }
    }
}
