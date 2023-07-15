using MobileDiffusion.Interfaces.Services;
using SkiaSharp;
using System.IO;

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

    public (byte[] Bytes, int ActualWidth, int ActualHeight) GetResizedImageStreamBytes(Stream stream, int width, int height, bool forceExactSize = false, bool filterImage = false)
    {
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

            var bitmap = SKBitmap.Decode(codec, info);

            if (forceExactSize)
            {
                return resizeBitmap(bitmap, width, height, filterImage);
            }

            if (bitmap.Width <= width &&
                bitmap.Height <= height)
            {
                stream.Seek(0, SeekOrigin.Begin);

                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                return (memoryStream.ToArray(), bitmap.Width, bitmap.Height);
            }

            // 1024 x 512 -> 512 x 512 = 512 x 256
            // 512 x 1024 -> 512 x 512 = 256 x 512
            // 1024 x 512 -> 512 x 1024 = 512 x 256

            var bitmapWidthIsBiggest = bitmap.Width > bitmap.Height;

            var bitmapRatio = bitmapWidthIsBiggest ?
                bitmap.Width / (float)bitmap.Height :
                bitmap.Height / (float)bitmap.Width;

            var targetWidth = 0;
            var targetHeight = 0;

            if (bitmapWidthIsBiggest)
            {
                targetWidth = width;
                var dividedHeight = height / bitmapRatio;

                var roundedHeight = Math.Round(dividedHeight / 64) * 64;
                targetHeight = (int)roundedHeight;
            }
            else
            {
                targetHeight = height;
                var dividedWidth = width / bitmapRatio;

                var roundedWidth = Math.Round(dividedWidth / 64) * 64;
                targetWidth = (int)roundedWidth;
            }

            return resizeBitmap(bitmap, targetWidth, targetHeight, filterImage);
        }
        catch
        {
            // TODO - Handle this
        }

        return (null, 0, 0);
    }

    private (byte[] Bytes, int ActualWidth, int ActualHeight) resizeBitmap(SKBitmap bitmap, int width, int height, bool filterImage = false)
    {
        var resized = bitmap.Resize(new SKSizeI(width, height), filterImage ? SKFilterQuality.High : SKFilterQuality.None);

        using (var memStream = new MemoryStream())
        {
            using (var skiaStream = new SKManagedWStream(memStream))
            {
                resized.Encode(skiaStream, SKEncodedImageFormat.Png, 100);

                memStream.Seek(0, SeekOrigin.Begin);
                return (memStream.ToArray(), resized.Width, resized.Height);
            }
        }
    }
}
