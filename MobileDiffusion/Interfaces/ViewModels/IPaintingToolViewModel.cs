using MobileDiffusion.Enums;
namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPaintingToolViewModel : IBaseViewModel
{
    public string Name { get; set; }

    public string IconCode { get; set; }

    public MaskEffect Effect { get; set; }

    public ToolType Type { get; set; }

    public bool IsLoading { get; set; }
}
