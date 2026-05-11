using SkiaSharp;

namespace Mogri.Interfaces.Services;

/// <summary>
/// Represents a stroke-based canvas action that can contribute to a patch mask.
/// </summary>
public interface ICanvasMaskStrokeAction : ICanvasRenderAction
{
    float BrushSize { get; }

    bool AddsToMask { get; }

    IReadOnlyList<SKPoint> Points { get; }
}