using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPromptStyleInfoPopupViewModel : IPopupBaseViewModel
{
    IPromptStyleViewModel PromptStyle { get; set; }

    IRelayCommand CloseCommand { get; }
}
