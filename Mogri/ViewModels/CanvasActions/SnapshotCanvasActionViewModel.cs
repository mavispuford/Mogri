using Mogri.Enums;
using SkiaSharp;

namespace Mogri.ViewModels;

/// <summary>
/// A marker action in the canvas history timeline. It does not render anything 
/// directly, but stores a reference to a snapshot on disk.
/// </summary>
public class SnapshotCanvasActionViewModel : CanvasActionViewModel
{
    public string SnapshotId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IncludesCanvasActions { get; set; }

    public SnapshotCanvasActionViewModel()
    {
        CanvasActionType = CanvasActionType.Snapshot;
    }

    /// <summary>
    /// Executes nothing, as a snapshot is just a marker in the history timeline.
    /// </summary>
    public override void Execute(SKCanvas canvas, SKImageInfo imageInfo, bool isSaving)
    {
        // No-op
    }

    public override CanvasActionViewModel Clone()
    {
        return new SnapshotCanvasActionViewModel
        {
            SnapshotId = this.SnapshotId,
            Description = this.Description,
            IncludesCanvasActions = this.IncludesCanvasActions,
            CanvasActionType = this.CanvasActionType,
            Order = this.Order
        };
    }
}
