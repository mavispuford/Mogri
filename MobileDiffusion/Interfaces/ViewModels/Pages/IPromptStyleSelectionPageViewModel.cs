using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels.Pages;

public interface IPromptStyleSelectionPageViewModel : IPageViewModel
{
    List<IPromptStyleViewModel> AvailablePromptStyles { get; set; }

    ObservableCollection<IPromptStyleViewModel> SelectedPromptStyles { get; set; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmCommand { get; }

    IAsyncRelayCommand<string> FilterCommand { get; }

    IRelayCommand ResetCommand { get; }

    IAsyncRelayCommand<IPromptStyleViewModel> ShowPromptStyleInfoCommand { get; }
}
