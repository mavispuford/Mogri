using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels.Pages;

public interface ILoraSelectionPageViewModel : IPageViewModel
{
    List<ILoraViewModel> AvailableLoras { get; set; }

    ObservableCollection<ILoraViewModel> SelectedLoras { get; set; }

    ILoraViewModel? LoraToAdd { get; set; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmCommand { get; }

    IRelayCommand<ILoraViewModel> RemoveCommand { get; }

    IRelayCommand ResetCommand { get; }
}
