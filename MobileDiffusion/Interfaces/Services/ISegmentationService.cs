using SkiaSharp;

namespace MobileDiffusion.Interfaces.Services;

public interface ISegmentationService
{
    Task DoSegmentation(SKBitmap bitmap);
}
