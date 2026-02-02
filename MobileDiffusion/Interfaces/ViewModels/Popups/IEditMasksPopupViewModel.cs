using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels.Popups;

public interface IEditMasksPopupViewModel : IPopupBaseViewModel
{
    ObservableCollection<IEditMaskItemViewModel> Items { get; set; }
    IAsyncRelayCommand ClearAllCommand { get; }
}
