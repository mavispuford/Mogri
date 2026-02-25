using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class PaintingToolViewModel : BaseViewModel, IPaintingToolViewModel
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string IconCode { get; set; }

    [ObservableProperty]
    public partial string IconImagePath { get; set; }

    [ObservableProperty]
    public partial MaskEffect Effect { get; set; }

    [ObservableProperty]
    public partial ToolType Type { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial List<ContextButtonType> ContextButtons { get; set; }
}
