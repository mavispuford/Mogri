using CommunityToolkit.Mvvm.Input;

namespace Mogri.Interfaces.ViewModels.Popups;

public interface IResultItemPopupViewModel : IPopupBaseViewModel
{
    IResultItemViewModel ResultItem { get; set; }

    IAsyncRelayCommand CloseCommand { get; }

    IAsyncRelayCommand SaveCommand { get; }

    IAsyncRelayCommand UseSeedCommand { get; }

    IAsyncRelayCommand ImageToImageCommand { get; }

    IAsyncRelayCommand SendToCanvasCommand { get; }

    IAsyncRelayCommand SwipeLeftCommand { get; }

    IAsyncRelayCommand SwipeRightCommand { get; }
}
