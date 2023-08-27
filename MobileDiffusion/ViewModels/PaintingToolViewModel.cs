using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;
using static MobileDiffusion.Models.MaskLine;

namespace MobileDiffusion.ViewModels;

public partial class PaintingToolViewModel : BaseViewModel, IPaintingToolViewModel
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _iconCode;

    [ObservableProperty]
    private MaskLineType _type;
}
