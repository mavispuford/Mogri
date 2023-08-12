using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class LoraViewModel : BaseViewModel, ILoraViewModel
{
    [ObservableProperty]
    private string _alias;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private float _strength = 1;
}
