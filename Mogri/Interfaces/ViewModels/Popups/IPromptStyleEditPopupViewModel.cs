using CommunityToolkit.Mvvm.Input;

namespace Mogri.Interfaces.ViewModels.Popups;

public interface IPromptStyleEditPopupViewModel : IPopupBaseViewModel
{
    IPromptStyleViewModel PromptStyle { get; set; }

    string EditName { get; set; }

    string EditPrompt { get; set; }

    string EditNegativePrompt { get; set; }

    IAsyncRelayCommand SaveCommand { get; }

    IAsyncRelayCommand CloseCommand { get; }
}
