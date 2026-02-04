using SkiaSharp;

namespace MobileDiffusion.Interfaces.Services;

public interface ISegmentationService
{
    SKColor MaskColor { get; }
    Task<bool> SetImage(SKBitmap bitmap, CancellationToken token);
    Task<SKBitmap?> DoSegmentation(SKPoint[] points, bool reset = false);
    void Reset();
    void UnloadModel();
}
