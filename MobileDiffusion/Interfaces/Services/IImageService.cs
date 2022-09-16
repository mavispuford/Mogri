namespace MobileDiffusion.Interfaces.Services;

public interface IImageService
{
    Task<MemoryStream> GetStreamFromContentTypeStringAsync(string imageString, CancellationToken token);
    Task<ImageSource> GetImageSourceFromContentTypeStringAsync(string imageString, CancellationToken token);
    byte[] GetResizedImageStreamBytes(Stream stream, int width, int height);
}
