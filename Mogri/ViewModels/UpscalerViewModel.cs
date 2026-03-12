using CommunityToolkit.Mvvm.ComponentModel;
using Mogri.Interfaces.ViewModels;

namespace Mogri.ViewModels;

public partial class UpscalerViewModel : BaseViewModel, IUpscalerViewModel
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string ModelName { get; set; }

    [ObservableProperty]
    public partial double Scale { get; set; }
}
