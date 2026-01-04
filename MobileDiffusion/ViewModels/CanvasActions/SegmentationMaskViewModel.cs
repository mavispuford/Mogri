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
            using var paint = new SKPaint();

            canvas.SaveLayer(paint);

            canvas.DrawBitmap(Bitmap, Bitmap.Info.Rect, imageInfo.Rect, paint);

            paint.BlendMode = SKBlendMode.SrcIn;

            if (!isSaving && Color.Alpha <= 0.1f && _bitmapOutlineShader != null)
            {
                paint.Shader = _bitmapOutlineShader;
                paint.Color = SKColors.White;
            }
            else
            {
                paint.Shader = null;
                paint.Color = Color.ToSKColor();
            }

            canvas.DrawPaint(paint);

            canvas.Restore();
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
