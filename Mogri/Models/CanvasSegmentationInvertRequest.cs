using SkiaSharp;

namespace Mogri.Models;

/// <summary>
/// Explicit inputs for segmentation-mask inversion, including the empty-mask bootstrap path.
/// </summary>
public sealed class CanvasSegmentationInvertRequest
{
    public SKBitmap? CurrentSegmentationBitmap { get; init; }

    public SKImageInfo? SourceImageInfo { get; init; }
}