using SkiaSharp;

namespace Mogri.Models;

/// <summary>
/// Holds the next segmentation bitmap produced by the coordinator.
/// </summary>
public sealed record CanvasSegmentationMaskUpdateResult(SKBitmap SegmentationBitmap);