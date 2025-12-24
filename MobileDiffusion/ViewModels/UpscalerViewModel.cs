using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class UpscalerViewModel : BaseViewModel, IUpscalerViewModel
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string ModelName { get; set; }

    [ObservableProperty]
    public partial double Scale { get; set; }
}
