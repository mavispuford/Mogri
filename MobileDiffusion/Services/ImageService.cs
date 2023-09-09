using MobileDiffusion.Interfaces.Services;
using SkiaSharp;

namespace MobileDiffusion.Services;

public class ImageService : IImageService
{
    public Task<MemoryStream> GetStreamFromContentTypeStringAsync(string imageString, CancellationToken token)
    {
        if (string.IsNullOrEmpty(imageString))
        {
            return Task.FromResult<MemoryStream>(null);
        }

        var matchResult = Constants.ImageDataRegex.Match(imageString);

        string toProcess = string.Empty;

        if (matchResult.Success)
        {
            toProcess = matchResult.Groups[Constants.ImageDataCaptureGroupData].Value;
        }
        else
        {
            toProcess = imageString;
        }

        try
        {
            var imageBytes = Convert.FromBase64String(toProcess);

            if (token.IsCancellationRequested)
            {
                return Task.FromResult<MemoryStream>(null);
            }

            return Task.FromResult(new MemoryStream(imageBytes));
        }
        catch
        {
            // TODO - Handle exceptions
        }
        
        return Task.FromResult<MemoryStream>(null);
    }

    public async Task<ImageSource> GetImageSourceFromContentTypeStringAsync(string imageString, CancellationToken token)
    {
        var stream = await GetStreamFromContentTypeStringAsync(imageString, token);

        if (stream == null || token.IsCancellationRequested)
        {
            return null;
        }

        return ImageSource.FromStream(() => stream);
    }

    public SKBitmap GetSkBitmapFromStream(Stream stream)
    {
        if (stream == null)
        {
            return null;
        }

        try
        {
            // Instead of a simple SKBitmap.Decode() call, we're using a codec and SKImageInfo with Unpremul for the
            // AlphaType to preserve masked image pixel data

            var codec = SKCodec.Create(stream);
            var info = new SKImageInfo
            {
                AlphaType = SKAlphaType.Unpremul,
                ColorSpace = codec.Info.ColorSpace,
                ColorType = codec.Info.ColorType,
                Height = codec.Info.Height,
                Width = codec.Info.Width,
            };

            return SKBitmap.Decode(codec, info);
        }
        catch
        {
            return null;
        }
    }

    public SKBitmap GetResizedSKBitmap(SKBitmap bitmap, int width, int height, bool forceExactSize = false, bool filterImage = false, bool onlyIfLarger = false)
    {
        try
        {
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

            // Scale to target, maintaining the aspect ratio
            // 1024 x 512 -> 512 x 512 = 512 x 256
            // 512 x 1024 -> 512 x 512 = 256 x 512
            // 1024 x 512 -> 512 x 1024 = 512 x 256

            var landscape = bitmap.Width > bitmap.Height;

            var bitmapRatio = landscape ?
                bitmap.Width / (float)bitmap.Height :
                bitmap.Height / (float)bitmap.Width;

            var targetWidth = 0;
            var targetHeight = 0;

            if (landscape)
            {
                targetWidth = width;
                var dividedHeight = width / bitmapRatio;

                targetHeight = (int)dividedHeight;
            }
            else
            {
                targetHeight = height;
                var dividedWidth = height / bitmapRatio;

                targetWidth = (int)dividedWidth;
            }

            return resizeBitmap(bitmap, targetWidth, targetHeight, filterImage);
        }
        catch
        {
            // TODO - Handle this
        }

        return null;
    }

    public (byte[] Bytes, int ActualWidth, int ActualHeight) GetResizedImageStreamBytes(Stream stream, int width, int height, bool forceExactSize = false, bool filterImage = false, bool onlyIfLarger = false)
    {
        try
        {
            var bitmap = GetSkBitmapFromStream(stream);

            var resizedBitmap = GetResizedSKBitmap(bitmap, width, height, forceExactSize, filterImage, onlyIfLarger);

            using (var memStream = new MemoryStream())
            {
                using (var skiaStream = new SKManagedWStream(memStream))
                {
                    resizedBitmap.Encode(skiaStream, SKEncodedImageFormat.Png, 100);

                    memStream.Seek(0, SeekOrigin.Begin);
                    return (memStream.ToArray(), resizedBitmap.Width, resizedBitmap.Height);
                }
            }
        }
        catch
        {
            // TODO - Handle this
        }

        return (null, 0, 0);
    }

    private SKBitmap resizeBitmap(SKBitmap bitmap, int width, int height, bool filterImage = false)
    {
        return bitmap.Resize(new SKSizeI(width, height), filterImage ? SKFilterQuality.High : SKFilterQuality.None);
    }
}
