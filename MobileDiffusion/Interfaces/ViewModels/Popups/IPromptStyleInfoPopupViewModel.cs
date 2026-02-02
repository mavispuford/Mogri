using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels.Popups;

public interface IPromptStyleInfoPopupViewModel : IPopupBaseViewModel
{
    IPromptStyleViewModel PromptStyle { get; set; }

    IAsyncRelayCommand CloseCommand { get; }
}
