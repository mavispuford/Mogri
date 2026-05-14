using Mogri.Interfaces.Services;
using SkiaSharp;

namespace Mogri.Models;

/// <summary>
/// Explicit inputs for patching the source bitmap using the current canvas action mask state.
/// </summary>
public sealed class CanvasPatchWorkflowRequest
{
    public required SKBitmap SourceBitmap { get; init; }

    public required IReadOnlyCollection<ICanvasRenderAction> CanvasActions { get; init; }

    public bool UseLastOnly { get; init; }
}