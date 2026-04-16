using CommunityToolkit.Mvvm.ComponentModel;
using Mogri.Interfaces.ViewModels;

namespace Mogri.ViewModels;

public partial class LoraViewModel : BaseViewModel, ILoraViewModel
{
    [ObservableProperty]
    public partial string Alias { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial double Strength { get; set; } = 1d;
}
