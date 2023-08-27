using static MobileDiffusion.Models.MaskLine;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPaintingToolViewModel : IBaseViewModel
{
    public string Name { get; set; }

    public string IconCode { get; set; }

    public MaskLineType Type { get; set; }
}
