using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Helpers;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using System.Text.Json.Serialization;

namespace MobileDiffusion.ViewModels;

public partial class SegmentationMaskViewModel : CanvasActionViewModel
{
    private SKShader _bitmapOutlineShader;

    [ObservableProperty]
    [property: JsonIgnore]
    public partial SKBitmap Bitmap { get; set; }

    public byte[] BitmapBytes
    {
        get => Bitmap?.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        set
        {
            if (value != null)
                Bitmap = SKBitmap.Decode(value);
        }
    }

    [ObservableProperty]
    public partial Color Color { get; set; }

    [ObservableProperty]
    public partial float Alpha { get; set; }

    public override void Execute(SKCanvas canvas, SKImageInfo imageInfo, bool isSaving)
    {
        if (Bitmap != null)
        {
            using var paint = new SKPaint();

            canvas.SaveLayer(paint);

            canvas.DrawBitmap(Bitmap, Bitmap.Info.Rect, imageInfo.Rect, paint);

            paint.BlendMode = SKBlendMode.SrcIn;

            if (!isSaving && Alpha <= 0.1f && _bitmapOutlineShader != null)
            {
                paint.Shader = _bitmapOutlineShader;
                paint.Color = SKColors.White;
            }
            else
            {
                paint.Shader = null;
                paint.Color = Color.ToSKColor().WithAlpha((byte)(Alpha * 255));
            }

            canvas.DrawPaint(paint);

            canvas.Restore();
        }
    }

    partial void OnColorChanged(Color value)
    {
        if (value != null)
        {
            _bitmapOutlineShader = MaskHelper.CreateMaskBitmapShaderLines(value.ToSKColor().WithAlpha((byte)(Alpha * 255)));
        }
    }

    partial void OnAlphaChanged(float value)
    {
        if (Color != null)
        {
            OnColorChanged(Color);
        }
    }
}
