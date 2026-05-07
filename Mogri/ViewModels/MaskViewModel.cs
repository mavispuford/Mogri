using Mogri.ViewModels;

namespace Mogri.ViewModels;

public class MaskViewModel : BaseViewModel
{
    public List<MaskLineViewModel> Lines { get; set; } = new();
    public List<SegmentationMaskViewModel> SegmentationMasks { get; set; } = new();
    public List<SnapshotCanvasActionViewModel> Snapshots { get; set; } = new();
    public List<TextElementViewModel> TextElements { get; set; } = new();
}
