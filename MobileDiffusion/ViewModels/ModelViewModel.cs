using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class ModelViewModel : BaseViewModel, IModelViewModel
{
    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _key;

    public override string ToString()
    {
        return DisplayName;
    }
}
