using ColorMine.ColorSpaces.Comparisons;
using ColorMine.ColorSpaces;
using Mogri.Helpers;
using Mogri.Interfaces.Services;
using SkiaSharp;

using Microsoft.Extensions.Logging;

namespace Mogri.Services;

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

        var bitmap = GetSkBitmapFromStream(stream);
        if (bitmap != null)
        {
            return new SkiaSharp.Views.Maui.Controls.SKBitmapImageSource
            {
                Bitmap = bitmap
            };
        }

        return null;
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
            return ImagePayloadHelper.GetResizedBitmap(bitmap, width, height, forceExactSize, filterImage, onlyIfLarger);
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
            return ImagePayloadHelper.CreateThumbnailString(bitmap, contentType, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get thumbnail string from bitmap");
        }

        return null;
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

    public List<Color>? ExtractColorPalette(SKBitmap? bitmap, int targetNumber = 30)
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

        var width = smallBitmap.Width;
        var height = smallBitmap.Height;

        var allColors = new HashSet<Rgb>();

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                var color = smallBitmap.GetPixel(col, row);
                allColors.Add(new Rgb
                {
                    R = color.Red,
                    G = color.Green,
                    B = color.Blue
                });
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
        var hsv = color.To<Hsv>();
        var hsl = hsv.To<Hsl>();
        var lum = hsl.L;

        int h2 = (int)(hsv.H * repetitions);
        int lum2 = (int)(lum * repetitions);
        int v2 = (int)(hsv.V * repetitions);

        return (h2, lum2, v2);
    }
}
