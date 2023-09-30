using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class UpscalerViewModel : BaseViewModel, IUpscalerViewModel
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _modelName;

    [ObservableProperty]
    private double _scale;
}
