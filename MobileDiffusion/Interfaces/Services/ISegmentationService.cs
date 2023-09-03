using SkiaSharp;

namespace MobileDiffusion.Interfaces.Services;

public interface ISegmentationService
{
    Task<bool> SetImage(SKBitmap bitmap, CancellationToken token);
    Task<SKBitmap> DoSegmentation(SKPoint location);
}
