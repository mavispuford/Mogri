using SkiaSharp;

namespace Mogri.Helpers;

/// <summary>
/// Prepares soft-alpha mask images for ComfyUI uploads.
/// </summary>
public static class ComfyUiMaskHelper
{
    public static byte[]? CreateSoftMaskPng(byte[]? imageBytes, int blurRadius)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return null;
        }

        if (blurRadius <= 0)
        {
            return imageBytes;
        }

        SKBitmap? sourceBitmap;
        try
        {
            sourceBitmap = SKBitmap.Decode(imageBytes);
        }
        catch (ArgumentException)
        {
            return null;
        }

        using (sourceBitmap)
        {
            if (sourceBitmap == null)
            {
                return null;
            }

            return CreateSoftMaskPng(sourceBitmap, blurRadius);
        }
    }

    private static byte[]? CreateSoftMaskPng(SKBitmap sourceBitmap, int blurRadius)
    {
        using var softenedMask = new SKBitmap(sourceBitmap.Width, sourceBitmap.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        using (var canvas = new SKCanvas(softenedMask))
        {
            canvas.Clear(SKColors.Transparent);

            using var blurFilter = SKImageFilter.CreateBlur(
                Math.Min(blurRadius, 64),
                Math.Min(blurRadius, 64),
                SKShaderTileMode.Decal);
            using var paint = new SKPaint
            {
                ImageFilter = blurFilter
            };

            canvas.DrawBitmap(sourceBitmap, 0, 0, SKSamplingOptions.Default, paint);
        }

        return softenedMask.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }
}