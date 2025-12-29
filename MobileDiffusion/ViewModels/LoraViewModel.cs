using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class LoraViewModel : BaseViewModel, ILoraViewModel
{
    [ObservableProperty]
    public partial string Alias { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial double Strength { get; set; } = 1d;
}
