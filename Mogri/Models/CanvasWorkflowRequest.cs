using Mogri.Enums;
using Mogri.Interfaces.Services;
using SkiaSharp;

namespace Mogri.Models;

/// <summary>
/// Explicit inputs for building canvas workflow payloads without leaking viewmodel state into the coordinator.
/// </summary>
public class CanvasWorkflowRequest
{
    public required SKBitmap SourceBitmap { get; init; }

    public required IReadOnlyCollection<ICanvasRenderAction> CanvasActions { get; init; }

    public CanvasUseMode CanvasUseMode { get; init; }

    public bool HasMaskActions { get; init; }
}