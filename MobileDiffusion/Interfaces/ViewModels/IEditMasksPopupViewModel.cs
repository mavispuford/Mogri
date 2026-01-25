using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IEditMasksPopupViewModel : IPopupBaseViewModel
{
    ObservableCollection<IEditMaskItemViewModel> Items { get; set; }
    IAsyncRelayCommand ClearAllCommand { get; }
}
