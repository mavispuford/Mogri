using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class HistoryItemViewModel : BaseViewModel, IHistoryItemViewModel
{
    [ObservableProperty]
    private ImageSource _imageSource;
}
