using SkiaSharp;

namespace Mogri.Models;

/// <summary>
/// Holds the patched bitmap returned from the patch workflow.
/// </summary>
public sealed record CanvasPatchWorkflowResult(SKBitmap? PatchedBitmap);