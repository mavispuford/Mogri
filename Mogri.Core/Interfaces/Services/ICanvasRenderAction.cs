using SkiaSharp;

namespace Mogri.Interfaces.Services;

/// <summary>
/// Represents a canvas action that can render itself into a workflow bitmap surface.
/// </summary>
public interface ICanvasRenderAction
{
    void Execute(SKCanvas canvas, SKImageInfo imageInfo, bool isSaving);
}