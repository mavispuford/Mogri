using System.Collections.Generic;
using System.Threading.Tasks;
using Mogri.Models;
using Mogri.ViewModels;
using SkiaSharp;

namespace Mogri.Interfaces.Services;

/// <summary>
/// Manages the persistence of canvas bitmap snapshots to local disk (CacheDirectory).
/// </summary>
public interface ICanvasHistoryService
{
    /// <summary>
    /// Gets the number of tracked snapshots currently available.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Saves the bitmap as a PNG image and optionally serializes the canvas actions to JSON.
    /// </summary>
    /// <param name="bitmap">The current SKBitmap to snapshot.</param>
    /// <param name="canvasActions">The optional list of CanvasActions to serialize.</param>
    /// <param name="textElements">The optional list of text elements to serialize.</param>
    /// <returns>A string representing the snapshot ID (GUID).</returns>
    Task<string> SaveSnapshotAsync(SKBitmap bitmap, IList<CanvasActionViewModel>? canvasActions = null, IList<TextElementViewModel>? textElements = null);

    /// <summary>
    /// Loads the bitmap and optionally the canvas actions from disk.
    /// Also deletes the physical files for the given ID after loading (consumes the snapshot).
    /// </summary>
    /// <param name="snapshotId">The snapshot ID to restore.</param>
    /// <returns>A tuple containing the deserialized SKBitmap, the optional list of CanvasActions, and the optional list of text elements.</returns>
    Task<(SKBitmap? Bitmap, List<CanvasActionViewModel>? CanvasActions, List<TextElementViewModel>? TextElements)> RestoreSnapshotAsync(string snapshotId);

    /// <summary>
    /// Deletes the snapshot files for a given ID without restoring.
    /// </summary>
    /// <param name="snapshotId">The snapshot ID to delete.</param>
    /// <returns></returns>
    Task DeleteSnapshotAsync(string snapshotId);

    /// <summary>
    /// Deletes all snapshot files from the designated snapshot directory.
    /// </summary>
    /// <returns></returns>
    Task ClearAllAsync();
}
