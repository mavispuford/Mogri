using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IResultItemPopupViewModel : IPopupBaseViewModel
{
    IResultItemViewModel ResultItem { get; set; }

    IRelayCommand CloseCommand { get; }

    IAsyncRelayCommand SaveCommand { get; }
}
