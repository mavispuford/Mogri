using SkiaSharp;

namespace Mogri.Interfaces.Services;

public interface ISegmentationService
{
    SKColor MaskColor { get; }
    Task<bool> SetImage(SKBitmap bitmap, CancellationToken token);
    Task<SKBitmap?> DoSegmentation(SKPoint[] points, bool reset = false);
    SKBitmap InvertMask(SKBitmap currentMask);
    void Reset();
    void UnloadModel();
}
