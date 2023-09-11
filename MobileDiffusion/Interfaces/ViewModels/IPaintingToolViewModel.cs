using MobileDiffusion.Enums;
using MobileDiffusion.ViewModels.CanvasContextButtons;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPaintingToolViewModel : IBaseViewModel
{
    string Name { get; set; }

    string IconCode { get; set; }

    MaskEffect Effect { get; set; }

    ToolType Type { get; set; }

    bool IsLoading { get; set; }

    List<CanvasContextButtonViewModel> ContextButtons { get; set; }
}
