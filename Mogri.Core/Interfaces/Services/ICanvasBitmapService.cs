using SkiaSharp;

namespace Mogri.Interfaces.Services;

/// <summary>
/// Provides canvas-specific bitmap composition operations used by the canvas workflows.
/// </summary>
public interface ICanvasBitmapService
{
    SKBitmap? CreateBlackAndWhiteMask(SKBitmap? maskBitmap);

    SKBitmap? CreateMaskBitmapFromSegmentationMask(SKBitmap? segmentationBitmap);

    SKBitmap? CreateMaskedBitmap(SKBitmap? sourceBitmap, SKBitmap? maskBitmap);

    SKBitmap? GetCroppedBitmap(SKBitmap? bitmap, SKRect cropRect, double cropScale, float cropSize);

    SKBitmap? StitchBitmapIntoSource(SKBitmap? bitmap, SKBitmap? bitmapToStitchIn, SKRect rect, double rectScale);
}