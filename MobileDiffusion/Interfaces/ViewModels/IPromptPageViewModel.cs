using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPromptPageViewModel : IPageViewModel
{
    string Prompt { get; set; }

    string PromptPlaceholder { get; set; }

    string NegativePrompt { get; set; }

    IAsyncRelayCommand ConfirmCommand { get; }
}
