namespace Mogri.Models;

/// <summary>
/// Holds the navigation parameters produced by a canvas workflow for downstream pages.
/// </summary>
public sealed record CanvasWorkflowNavigationResult(Dictionary<string, object> Parameters);