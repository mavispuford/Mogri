using SkiaSharp;

namespace Mogri.Models;

/// <summary>
/// Explicit inputs for interactive segmentation execution without leaking canvas viewmodel state into the coordinator.
/// </summary>
public sealed class CanvasSegmentationRequest
{
    public required SKPoint[] Points { get; init; }

    public SKBitmap? CurrentSegmentationBitmap { get; init; }

    public bool SegmentationAdd { get; init; }
}