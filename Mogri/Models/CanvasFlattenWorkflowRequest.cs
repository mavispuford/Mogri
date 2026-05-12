using Mogri.Interfaces.Services;
using SkiaSharp;

namespace Mogri.Models;

/// <summary>
/// Explicit inputs for flattening the current canvas paint and mask layers into a replacement bitmap.
/// </summary>
public sealed class CanvasFlattenWorkflowRequest
{
    public required SKBitmap SourceBitmap { get; init; }

    public SKBitmap? PreparedSourceBitmap { get; init; }

    public required IReadOnlyCollection<ICanvasRenderAction> CanvasActions { get; init; }

    public bool HasMaskActions { get; init; }
}