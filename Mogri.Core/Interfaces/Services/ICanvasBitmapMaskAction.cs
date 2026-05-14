using SkiaSharp;

namespace Mogri.Interfaces.Services;

/// <summary>
/// Represents a bitmap-backed canvas action that can contribute to a patch mask.
/// </summary>
public interface ICanvasBitmapMaskAction : ICanvasRenderAction
{
    SKBitmap? Bitmap { get; }
}