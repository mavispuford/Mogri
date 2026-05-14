using Mogri.Models;
using SkiaSharp;

namespace Mogri.Interfaces.Coordinators;

/// <summary>
/// Coordinates segmentation image readiness, latest-wins setup, and interactive mask operations.
/// </summary>
public interface ICanvasSegmentationCoordinator
{
    event EventHandler<CanvasSegmentationImageStateChangedEventArgs>? ImageStateChanged;

    Task SetImageAsync(SKBitmap bitmap);

    Task<CanvasSegmentationMaskUpdateResult?> DoSegmentationAsync(CanvasSegmentationRequest request);

    Task<CanvasSegmentationMaskUpdateResult?> InvertMaskAsync(CanvasSegmentationInvertRequest request);

    Task<SKBitmap?> CreateMaskBitmapFromSegmentationAsync(SKBitmap segmentationBitmap);

    void Reset();
}