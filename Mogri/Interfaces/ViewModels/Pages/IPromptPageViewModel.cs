using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Mogri.Interfaces.ViewModels.Pages;

public interface IPromptPageViewModel : IPageViewModel
{
    string? Prompt { get; set; }

    string? PromptPlaceholder { get; set; }

    string? NegativePrompt { get; set; }

    List<IPromptStyleViewModel> AvailablePromptStyles { get; set; }

    List<ILoraViewModel> AvailableLoras { get; set; }

    ObservableCollection<ILoraViewModel> SelectedLoras { get; set; }

    ObservableCollection<IPromptStyleViewModel> SelectedPromptStyles { get; set; }

    IRelayCommand ResetPageCommand { get; }

    IAsyncRelayCommand ConfirmCommand { get; }

    IRelayCommand<IPromptStyleViewModel> RemovePromptStyleCommand { get; }

    IRelayCommand<ILoraViewModel> RemoveLoraCommand { get; }

    IAsyncRelayCommand ShowLoraSelectionPageCommand { get; }

    IAsyncRelayCommand ShowPromptStyleCreationPromptCommand { get; }

    IAsyncRelayCommand ShowPromptStyleExtractionPromptCommand { get; }

    IAsyncRelayCommand ShowPromptStyleSelectionPageCommand { get; }

}
