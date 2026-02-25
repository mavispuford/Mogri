using MobileDiffusion.Enums;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPaintingToolViewModel : IBaseViewModel
{
    string Name { get; set; }

    string IconCode { get; set; }

    string IconImagePath { get; set; }

    MaskEffect Effect { get; set; }

    ToolType Type { get; set; }

    bool IsLoading { get; set; }

    List<ContextButtonType> ContextButtons { get; set; }
}
