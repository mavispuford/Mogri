using SkiaSharp;

namespace Mogri.Models;

/// <summary>
/// Adds crop bounds to the base canvas workflow payload inputs.
/// </summary>
public sealed class CanvasCropWorkflowRequest : CanvasWorkflowRequest
{
    public SKRect BoundingBox { get; init; }

    public double BoundingBoxScale { get; init; }

    public float BoundingBoxSize { get; init; }
}