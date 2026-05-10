using Mogri.Models;
using SkiaSharp;

namespace Mogri.Helpers;

/// <summary>
/// Builds encoded image payloads for workflow and navigation handoff.
/// </summary>
public static class ImagePayloadHelper
{
    private const string DefaultContentType = "image/png";

    public static string? CreateImageDataString(SKBitmap? bitmap, string? contentType = DefaultContentType)
    {
        if (bitmap == null)
        {
            return null;
        }

        using var memStream = new MemoryStream();
        using var skiaStream = new SKManagedWStream(memStream);

        bitmap.Encode(skiaStream, SKEncodedImageFormat.Png, 100);

        memStream.Seek(0, SeekOrigin.Begin);
        var imageBytes = memStream.ToArray();
        var imageString = Convert.ToBase64String(imageBytes);

        return string.Format(Constants.ImageDataFormat, normalizeContentType(contentType), imageString);
    }

    public static string? CreateThumbnailString(SKBitmap? bitmap, string? contentType = DefaultContentType, int width = 256, int height = 256)
    {
        var thumbnailBitmap = GetResizedBitmap(bitmap, width, height, forceExactSize: false, filterImage: false, onlyIfLarger: true);

        using var ownedThumbnailBitmap = thumbnailBitmap != null && !ReferenceEquals(thumbnailBitmap, bitmap)
            ? thumbnailBitmap
            : null;

        return CreateImageDataString(thumbnailBitmap, contentType);
    }

    public static SKBitmap? GetResizedBitmap(SKBitmap? bitmap, int width, int height, bool forceExactSize = false, bool filterImage = false, bool onlyIfLarger = false)
    {
        if (bitmap == null)
        {
            return null;
        }

        if (forceExactSize)
        {
            return resizeBitmap(bitmap, width, height, filterImage);
        }

        if (bitmap.Width == width &&
            bitmap.Height == height)
        {
            return bitmap;
        }

        if (onlyIfLarger &&
            bitmap.Width < width &&
            bitmap.Height < height)
        {
            return bitmap;
        }

        var landscape = bitmap.Width > bitmap.Height;

        var bitmapRatio = landscape
            ? bitmap.Width / (float)bitmap.Height
            : bitmap.Height / (float)bitmap.Width;

        var targetWidth = 0;
        var targetHeight = 0;

        if (landscape)
        {
            targetWidth = width;
            targetHeight = (int)(width / bitmapRatio);
        }
        else
        {
            targetHeight = height;
            targetWidth = (int)(height / bitmapRatio);
        }

        return resizeBitmap(bitmap, targetWidth, targetHeight, filterImage);
    }

    public static ImageTransferPayload? CreateConstrainedPayload(SKBitmap? bitmap, string? contentType = DefaultContentType)
    {
        if (bitmap == null)
        {
            return null;
        }

        var imageDataString = CreateImageDataString(bitmap, contentType);
        if (string.IsNullOrEmpty(imageDataString))
        {
            return null;
        }

        var constrainedDimensions = MathHelper.GetAspectCorrectConstrainedDimensions(bitmap.Width, bitmap.Height, 0, 0, MathHelper.DimensionConstraint.ClosestMatch);

        return new ImageTransferPayload(
            constrainedDimensions.Width,
            constrainedDimensions.Height,
            imageDataString,
            CreateThumbnailString(bitmap, contentType));
    }

    public static ImageTransferPayload? CreateFixedPayload(SKBitmap? bitmap, double width, double height, string? contentType = DefaultContentType)
    {
        if (bitmap == null)
        {
            return null;
        }

        var imageDataString = CreateImageDataString(bitmap, contentType);
        if (string.IsNullOrEmpty(imageDataString))
        {
            return null;
        }

        return new ImageTransferPayload(
            width,
            height,
            imageDataString,
            CreateThumbnailString(bitmap, contentType));
    }

    public static bool HasVisiblePixels(SKBitmap? bitmap)
    {
        if (bitmap == null)
        {
            return false;
        }

        for (var row = 0; row < bitmap.Height; row++)
        {
            for (var col = 0; col < bitmap.Width; col++)
            {
                if (bitmap.GetPixel(col, row).Alpha > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static SKBitmap resizeBitmap(SKBitmap bitmap, int width, int height, bool filterImage)
    {
        return bitmap.Resize(new SKSizeI(width, height), filterImage
            ? new SKSamplingOptions(SKCubicResampler.Mitchell)
            : new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
    }

    private static string normalizeContentType(string? contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            ? DefaultContentType
            : contentType;
    }
}