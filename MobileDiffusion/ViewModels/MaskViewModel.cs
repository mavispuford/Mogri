using MobileDiffusion.ViewModels;

namespace MobileDiffusion.ViewModels;

public class MaskViewModel : BaseViewModel
{
    public List<MaskLineViewModel> Lines { get; set; } = new();
}
