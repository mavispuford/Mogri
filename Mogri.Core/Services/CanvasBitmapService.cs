using System.Runtime.InteropServices;
using Mogri.Interfaces.Services;
using SkiaSharp;

namespace Mogri.Services;

/// <summary>
/// Performs canvas bitmap composition for mask generation, merging, cropping, and stitching.
/// </summary>
public sealed class CanvasBitmapService : ICanvasBitmapService
{
    public SKBitmap? CreateBlackAndWhiteMask(SKBitmap? maskBitmap)
    {
        if (maskBitmap == null)
        {
            return null;
        }

        var resultBitmap = new SKBitmap(maskBitmap.Width, maskBitmap.Height, maskBitmap.ColorType, SKAlphaType.Unpremul);

        // Get the raw byte spans
        var srcBytes = maskBitmap.GetPixelSpan();
        var destBytes = resultBitmap.GetPixelSpan();

        // Cast the byte spans safely into SKColor spans
        var srcPixels = MemoryMarshal.Cast<byte, SKColor>(srcBytes);
        var destPixels = MemoryMarshal.Cast<byte, SKColor>(destBytes);

        // Pre-allocate colors outside the loop
        var transparentWhite = new SKColor(255, 255, 255, 0);
        var opaqueBlack = new SKColor(0, 0, 0, 255);

        // Iterate through the properly cast span
        for (int i = 0; i < srcPixels.Length; i++)
        {
            destPixels[i] = srcPixels[i].Alpha == 0 ? transparentWhite : opaqueBlack;
        }

        return resultBitmap;
    }

    public SKBitmap? CreateMaskBitmapFromSegmentationMask(SKBitmap? segmentationBitmap)
    {
        if (segmentationBitmap == null)
        {
            return null;
        }

        var resultBitmap = new SKBitmap(segmentationBitmap.Info.Width, segmentationBitmap.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        using (var canvas = new SKCanvas(resultBitmap))
        {
            canvas.Clear(SKColors.Transparent);

            using var paint = new SKPaint();
            paint.ColorFilter = SKColorFilter.CreateBlendMode(SKColors.White, SKBlendMode.SrcIn);
            canvas.DrawBitmap(segmentationBitmap, 0, 0, paint);
        }

        return resultBitmap;
    }

    /// <summary>
    /// Composites the rendered mask layer over the source bitmap using standard alpha blending.
    /// This delegates to SkiaSharp's canvas compositing (SrcOver) so premultiplied alpha from the
    /// rendered layer is handled consistently with the on-canvas rendering path.
    /// </summary>
    public SKBitmap? CreateMaskedBitmap(SKBitmap? sourceBitmap, SKBitmap? maskBitmapOrig)
    {
        if (sourceBitmap == null ||
            maskBitmapOrig == null)
        {
            return null;
        }

        var maskBitmap = (maskBitmapOrig.Width == sourceBitmap.Width && maskBitmapOrig.Height == sourceBitmap.Height)
            ? maskBitmapOrig
            : maskBitmapOrig.Resize(new SKImageInfo(sourceBitmap.Width, sourceBitmap.Height), new SKSamplingOptions(SKCubicResampler.Mitchell));

        if (maskBitmap == null)
        {
            return null;
        }

        using var ownedMaskBitmap = !ReferenceEquals(maskBitmap, maskBitmapOrig)
            ? maskBitmap
            : null;

        var resultBitmap = new SKBitmap(sourceBitmap.Width, sourceBitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

        using (var canvas = new SKCanvas(resultBitmap))
        {
            canvas.DrawBitmap(sourceBitmap, 0, 0);
            canvas.DrawBitmap(maskBitmap, 0, 0);
        }

        return resultBitmap;
    }

    public SKBitmap? GetCroppedBitmap(SKBitmap? bitmap, SKRect cropRect, double cropScale, float cropSize)
    {
        if (bitmap == null)
        {
            return null;
        }

        if (cropRect.Width <= 0 ||
            cropRect.Height <= 0 ||
            cropScale <= 0d ||
            cropSize <= 0f)
        {
            return bitmap;
        }

        var left = (float)(cropRect.Left * cropScale);
        var top = (float)(cropRect.Top * cropScale);

        var adjustedRect = new SKRect(
            left,
            top,
            left + cropSize,
            top + cropSize);

        var info = new SKImageInfo
        {
            AlphaType = SKAlphaType.Unpremul,
            ColorSpace = bitmap.ColorSpace,
            ColorType = bitmap.ColorType,
            Height = (int)cropSize,
            Width = (int)cropSize,
        };

        var croppedBitmap = new SKBitmap(info);

        var source = new SKRect(adjustedRect.Left, adjustedRect.Top, adjustedRect.Right, adjustedRect.Bottom);
        var dest = new SKRect(0, 0, adjustedRect.Width, adjustedRect.Height);

        using (var canvas = new SKCanvas(croppedBitmap))
        {
            using var paint = new SKPaint
            {
                IsAntialias = false,
            };

            using var image = SKImage.FromBitmap(bitmap);
            canvas.DrawImage(image, source, dest, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), paint);
        }

        return croppedBitmap;
    }

    public SKBitmap? StitchBitmapIntoSource(SKBitmap? bitmap, SKBitmap? bitmapToStitchIn, SKRect rect, double rectScale)
    {
        if (bitmap == null)
        {
            return null;
        }

        if (bitmapToStitchIn == null)
        {
            return bitmap;
        }

        var info = new SKImageInfo
        {
            AlphaType = bitmap.AlphaType,
            ColorSpace = bitmap.ColorSpace,
            ColorType = bitmap.ColorType,
            Height = bitmap.Height,
            Width = bitmap.Width,
        };

        var resultBitmap = new SKBitmap(info);

        SKRect adjustedRect;

        if (rect.Width == 0 ||
            rect.Height == 0 ||
            rectScale <= 0d)
        {
            adjustedRect = bitmap.Info.Rect;
        }
        else
        {
            adjustedRect = new SKRect(
                (float)(rect.Left * rectScale),
                (float)(rect.Top * rectScale),
                (float)(rect.Right * rectScale),
                (float)(rect.Bottom * rectScale));
        }

        var source = new SKRect(0, 0, adjustedRect.Width, adjustedRect.Height);
        var dest = new SKRect(adjustedRect.Left, adjustedRect.Top, adjustedRect.Right, adjustedRect.Bottom);

        using (var canvas = new SKCanvas(resultBitmap))
        {
            using var paint = new SKPaint
            {
                IsAntialias = true,
            };

            canvas.DrawBitmap(bitmap, 0, 0);

            var toStitch = bitmapToStitchIn.Width != dest.Width || bitmapToStitchIn.Height != dest.Height
                ? bitmapToStitchIn.Resize(adjustedRect.Size.ToSizeI(), new SKSamplingOptions(SKCubicResampler.Mitchell))
                : bitmapToStitchIn;

            if (toStitch != null)
            {
                using var ownedToStitch = !ReferenceEquals(toStitch, bitmapToStitchIn)
                    ? toStitch
                    : null;
                using var image = SKImage.FromBitmap(toStitch);
                canvas.DrawImage(image, source, dest, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
            }
        }

        return resultBitmap;
    }
}