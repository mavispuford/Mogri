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

        if (matchResult.Success)
        {
            try
            {
                var imageBase64 = matchResult.Groups[Constants.ImageDataCaptureGroupData].Value;

                var imageBytes = Convert.FromBase64String(imageBase64);

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

    public byte[] GetResizedImageStreamBytes(Stream stream, int width, int height)
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

            if (bitmap.Width <= width &&
                bitmap.Height <= height)
            {
                stream.Seek(0, SeekOrigin.Begin);

                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
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

            var resized = bitmap.Resize(new SKSizeI(targetWidth, targetHeight), SKFilterQuality.None);

            using (var memStream = new MemoryStream())
            {
                using (var skiaStream = new SKManagedWStream(memStream))
                {
                    resized.Encode(skiaStream, SKEncodedImageFormat.Png, 100);

                    memStream.Seek(0, SeekOrigin.Begin);
                    return memStream.ToArray();
                }
            }
        }
        catch
        {

        }

        return null;
    }
}
