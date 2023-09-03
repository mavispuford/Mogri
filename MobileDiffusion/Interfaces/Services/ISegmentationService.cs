using SkiaSharp;

namespace MobileDiffusion.Interfaces.Services;

public interface ISegmentationService
{
    Task<bool> SetImage(SKBitmap bitmap);
    Task DoSegmentation(SKPoint location);
}
