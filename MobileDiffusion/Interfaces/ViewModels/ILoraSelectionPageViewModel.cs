using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface ILoraSelectionPageViewModel : IPageViewModel
{
    List<ILoraViewModel> AvailableLoras { get; set; }

    ObservableCollection<ILoraViewModel> SelectedLoras { get; set; }

    IRelayCommand<ILoraViewModel> AddCommand { get; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmCommand { get; }

    IRelayCommand ResetCommand { get; }
}
