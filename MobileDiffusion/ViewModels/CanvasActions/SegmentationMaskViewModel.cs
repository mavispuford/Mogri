using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Helpers;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using System.Text.Json.Serialization;

namespace MobileDiffusion.ViewModels;

public partial class SegmentationMaskViewModel : PaintActionViewModel
{
    [ObservableProperty]
    [property: JsonIgnore]
    public partial SKBitmap? Bitmap { get; set; }

    public byte[]? BitmapBytes
    {
        get => Bitmap?.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        set
        {
            if (value != null)
                Bitmap = SKBitmap.Decode(value);
        }
    }

    public override void Execute(SKCanvas canvas, SKImageInfo imageInfo, bool isSaving)
    {
        if (Bitmap != null)
        {
            using var paint = new SKPaint();

            canvas.SaveLayer(paint);

            // Draw to fill the full target area (which should be the Image Bounds provided by the caller)
            // Use Linear sampling to smooth the mask edges if scaling is involved
            using var image = SKImage.FromBitmap(Bitmap);
            canvas.DrawImage(image, Bitmap.Info.Rect, imageInfo.Rect, new SKSamplingOptions(SKFilterMode.Linear), paint);

            paint.BlendMode = SKBlendMode.SrcIn;

            // Use hatch pattern for low alpha visibility (Visual only, NOT when saving)
            if (!isSaving && Alpha <= 0.1f && _bitmapShader != null)
            {
                paint.Shader = _bitmapShader;
                paint.Color = SKColors.White;
            }
            else if (Noise > 0 && _noiseShader != null)
            {
                paint.Shader = _noiseShader;
                paint.Color = SKColors.White.WithAlpha((byte)(Alpha * 255));
            }
            // Fallback: Use the noise shader if configured (even when saving/exporting)
            else
            {
                paint.Shader = null;
                paint.Color = Color.ToSKColor().WithAlpha((byte)(Alpha * 255));
            }

            canvas.DrawPaint(paint);

            canvas.Restore();
        }
    }

    public override CanvasActionViewModel Clone()
    {
        return new SegmentationMaskViewModel
        {
            CanvasActionType = CanvasActionType,
            Order = Order,
            Color = Color,
            Alpha = Alpha,
            Noise = Noise,
            Bitmap = Bitmap?.Copy()
        };
    }


}
