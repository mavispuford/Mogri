using ColorMine.ColorSpaces.Comparisons;
using ColorMine.ColorSpaces;
using MobileDiffusion.Interfaces.Services;
using SkiaSharp;

using Microsoft.Extensions.Logging;

namespace MobileDiffusion.Services;

public class ImageService : IImageService
{
    private readonly ILogger<ImageService> _logger;

    public ImageService(ILogger<ImageService> logger)
    {
        _logger = logger;
    }

    public Task<MemoryStream?> GetStreamFromContentTypeStringAsync(string? imageString, CancellationToken token)
    {
        if (string.IsNullOrEmpty(imageString))
        {
            return Task.FromResult<MemoryStream?>(null);
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
                return Task.FromResult<MemoryStream?>(null);
            }

            return Task.FromResult<MemoryStream?>(new MemoryStream(imageBytes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert base64 image string to stream");
        }

        return Task.FromResult<MemoryStream?>(null);
    }

    public async Task<ImageSource?> GetImageSourceFromContentTypeStringAsync(string? imageString, CancellationToken token)
    {
        var stream = await GetStreamFromContentTypeStringAsync(imageString, token);

        if (stream == null || token.IsCancellationRequested)
        {
            return null;
        }

        return ImageSource.FromStream(() => stream);
    }

    public SKBitmap? GetSkBitmapFromStream(Stream? stream)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode SKBitmap from stream");
            return null;
        }
    }

    public SKBitmap? GetResizedSKBitmap(SKBitmap? bitmap, int width, int height, bool forceExactSize = false, bool filterImage = false, bool onlyIfLarger = false)
    {
        if (bitmap == null)
        {
            return null;
        }

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resize SKBitmap");
        }

        return null;
    }

    public (byte[]? Bytes, int ActualWidth, int ActualHeight) GetResizedImageStreamBytes(Stream? stream, int width, int height, bool forceExactSize = false, bool filterImage = false, bool onlyIfLarger = false)
    {
        try
        {
            var bitmap = GetSkBitmapFromStream(stream);

            if (bitmap == null)
            {
                return (null, 0, 0);
            }

            var resizedBitmap = GetResizedSKBitmap(bitmap, width, height, forceExactSize, filterImage, onlyIfLarger);

            if (resizedBitmap == null)
            {
                return (null, 0, 0);
            }

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resized image stream bytes");
        }

        return (null, 0, 0);
    }

    public string? GetThumbnailString(Stream? stream, string contentType, int width = 256, int height = 256)
    {
        try
        {
            if (stream == null) return null;
            stream.Seek(0, SeekOrigin.Begin);
            var bitmap = GetSkBitmapFromStream(stream);

            if (bitmap == null)
            {
                return null;
            }

            return GetThumbnailString(bitmap, contentType, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get thumbnail string from stream");
        }

        return null;
    }

    public string? GetThumbnailString(SKBitmap? bitmap, string contentType, int width = 256, int height = 256)
    {
        try
        {
            if (bitmap != null)
            {
                var thumbnailBitmap = GetResizedSKBitmap(bitmap, width, height, false, false, true);
                if (thumbnailBitmap != null)
                {
                    using var thumbStream = new MemoryStream();
                    using var skiaStream = new SKManagedWStream(thumbStream);
                    thumbnailBitmap.Encode(skiaStream, SKEncodedImageFormat.Png, 100);
                    thumbStream.Seek(0, SeekOrigin.Begin);
                    var thumbBytes = thumbStream.ToArray();
                    var thumbString = Convert.ToBase64String(thumbBytes);
                    return string.Format(Constants.ImageDataFormat, contentType ?? "image/png", thumbString);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get thumbnail string from bitmap");
        }

        return null;
    }

    private SKBitmap resizeBitmap(SKBitmap bitmap, int width, int height, bool filterImage = false)
    {
        return bitmap.Resize(new SKSizeI(width, height), filterImage ? new SKSamplingOptions(SKCubicResampler.Mitchell) : new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
    }

    private List<Rgb> filterColors(List<Rgb> sourceList, double minDeltaE, IColorSpaceComparison colorSpaceComparison)
    {
        var result = new List<Rgb>();

        foreach (var rgb in sourceList)
        {
            bool isDistinct = true;

            foreach (var existingRgb in result)
            {
                if (rgb.Compare(existingRgb, colorSpaceComparison) < minDeltaE)
                {
                    isDistinct = false;
                    break;
                }
            }

            if (isDistinct)
            {
                result.Add(rgb);
            }
        }

        return result;
    }

    unsafe public List<Color>? ExtractColorPalette(SKBitmap? bitmap, int targetNumber = 30)
    {
        if (bitmap == null)
        {
            return null;
        }

        const int maxSize = 128;

        var smallBitmap = GetResizedSKBitmap(bitmap, maxSize, maxSize, false, false, false);

        if (smallBitmap == null)
        {
            return null;
        }

        SKColorType colorType = smallBitmap.ColorType;

        var width = smallBitmap.Width;
        var height = smallBitmap.Height;

        var allColors = new HashSet<Rgb>();

        byte* bitmapPtr = (byte*)smallBitmap.GetPixels().ToPointer();

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                // Get color from bitmap
                byte byte1 = *bitmapPtr++;         // red or blue
                byte byte2 = *bitmapPtr++;         // green
                byte byte3 = *bitmapPtr++;         // blue or red
                byte byte4 = *bitmapPtr++;         // alpha

                if (colorType == SKColorType.Rgba8888)
                {
                    allColors.Add(new Rgb
                    {
                        R = byte1,
                        G = byte2,
                        B = byte3
                    });
                }
                else if (colorType == SKColorType.Bgra8888)
                {
                    allColors.Add(new Rgb
                    {
                        R = byte3,
                        G = byte2,
                        B = byte1
                    });
                }
            }
        }

        var colorSpaceComparison = new Cie1976Comparison();
        double minDeltaE = 10;

        var distinctRgbList = allColors.ToList();

        do
        {
            // Filter to distinct colors using an increasing Delta E each step
            distinctRgbList = filterColors(distinctRgbList, minDeltaE++, colorSpaceComparison);
        } while (distinctRgbList.Count > targetNumber);

        distinctRgbList.Sort((firstColor, secondColor) =>
        {
            var step1 = ClusteredHueLumValueStep(firstColor, 8);
            var step2 = ClusteredHueLumValueStep(secondColor, 8);

            if (step1.h2 != step2.h2)
            {
                return step1.h2.CompareTo(step2.h2);
            }
            else if (step1.lum != step2.lum)
            {
                return step1.lum.CompareTo(step2.lum);
            }
            else
            {
                return step1.v2.CompareTo(step2.v2);
            }
        });

        var distinctColors = distinctRgbList.Select(rgb => new Color((byte)rgb.R, (byte)rgb.G, (byte)rgb.B));

        return distinctColors.ToList();
    }

    private static (int h2, double lum, int v2) ClusteredHueLumValueStep(Rgb color, int repetitions = 1)
    {
        double lumTest = Math.Sqrt(0.241 * color.R + 0.691 * color.G + 0.068 * color.B);

        var hsv = color.To<Hsv>();
        var hsl = hsv.To<Hsl>();
        var lum = hsl.L;

        int h2 = (int)(hsv.H * repetitions);
        int lum2 = (int)(lum * repetitions);
        int v2 = (int)(hsv.V * repetitions);

        // TODO - Reverse luminosity sorting to smooth color layout
        //if (h2 % 2 == 1)
        //{
        //    v2 = repetitions - v2;
        //    lum2 = repetitions - lum2;
        //}

        return (h2, lum2, v2);
    }
}
