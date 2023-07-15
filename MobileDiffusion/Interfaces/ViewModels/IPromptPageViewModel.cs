using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPromptPageViewModel : IPageViewModel
{
    string Prompt { get; set; }

    string PromptPlaceholder { get; set; }

    string NegativePrompt { get; set; }

    List<IPromptStyleViewModel> AvailablePromptStyles { get; set; }

    ObservableCollection<IPromptStyleViewModel> SelectedPromptStyles { get; set; }

    IAsyncRelayCommand ConfirmCommand { get; }

    IRelayCommand<IPromptStyleViewModel> RemovePromptStyleCommand { get; }

    IAsyncRelayCommand ShowPromptStyleCreationPromptCommand { get; }

    IAsyncRelayCommand ShowPromptStyleExtractionPromptCommand { get; }

    IAsyncRelayCommand ShowPromptStyleSelectionPageCommand { get; }

}
