namespace Mogri.Models;

/// <summary>
/// Represents a snapshot of the canvas state at a point in time, including the rendered 
/// bitmap and optionally the serialized canvas actions that were active.
/// </summary>
public class CanvasSnapshot
{
    /// <summary>
    /// Gets or sets the path to the saved PNG image file in the CacheDirectory.
    /// </summary>
    public string BitmapFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional path to the serialized canvas actions JSON file.
    /// Used when a destructive operation clears the CanvasActions (e.g., Flatten).
    /// </summary>
    public string? ActionsFilePath { get; set; }
}
