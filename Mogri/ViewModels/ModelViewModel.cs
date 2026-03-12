using CommunityToolkit.Mvvm.ComponentModel;
using Mogri.Interfaces.ViewModels;

namespace Mogri.ViewModels;

public partial class ModelViewModel : BaseViewModel, IModelViewModel
{
    [ObservableProperty]
    public partial string DisplayName { get; set; }

    [ObservableProperty]
    public partial string Key { get; set; }

    public override string ToString()
    {
        return DisplayName;
    }
}
