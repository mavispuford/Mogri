using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class PaintingToolViewModel : BaseViewModel, IPaintingToolViewModel
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _iconCode;

    [ObservableProperty]
    private MaskEffect _effect;

    [ObservableProperty]
    private ToolType _type;

    [ObservableProperty]
    private bool _isLoading;
}
