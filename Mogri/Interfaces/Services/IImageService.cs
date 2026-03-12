using SkiaSharp;

namespace Mogri.Interfaces.Services;

public interface IImageService
{
    SKBitmap? GetSkBitmapFromStream(Stream? stream);
    Task<MemoryStream?> GetStreamFromContentTypeStringAsync(string? imageString, CancellationToken token);
    Task<ImageSource?> GetImageSourceFromContentTypeStringAsync(string? imageString, CancellationToken token);
    (byte[]? Bytes, int ActualWidth, int ActualHeight) GetResizedImageStreamBytes(Stream? stream, int width, int height, bool forceExactSize = false, bool filterImage = false, bool onlyIfLarger = false);
    SKBitmap? GetResizedSKBitmap(SKBitmap? bitmap, int width, int height, bool forceExactSize = false, bool filterImage = false, bool onlyIfLarger = false);
    string? GetThumbnailString(Stream? stream, string contentType, int width = 256, int height = 256);
    string? GetThumbnailString(SKBitmap? bitmap, string contentType, int width = 256, int height = 256);
    List<Color>? ExtractColorPalette(SKBitmap? bitmap, int targetNumber = 30);
}
