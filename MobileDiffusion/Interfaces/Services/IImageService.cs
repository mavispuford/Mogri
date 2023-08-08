using SkiaSharp;

namespace MobileDiffusion.Interfaces.Services;

public interface IImageService
{
    SKBitmap GetSkBitmapFromStream(Stream stream);
    Task<MemoryStream> GetStreamFromContentTypeStringAsync(string imageString, CancellationToken token);
    Task<ImageSource> GetImageSourceFromContentTypeStringAsync(string imageString, CancellationToken token);
    (byte[] Bytes, int ActualWidth, int ActualHeight) GetResizedImageStreamBytes(Stream stream, int width, int height, bool forceExactSize = false, bool filterImage = false);
}
