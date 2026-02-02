using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels.Popups;

public interface IHistoryItemPopupViewModel : IPopupBaseViewModel
{
    IHistoryItemViewModel HistoryItem { get; set; }

    ImageSource FullImageSource { get; set; }

    IAsyncRelayCommand DeleteCommand { get; }

    IAsyncRelayCommand CloseCommand { get; }

    IAsyncRelayCommand SaveCommand { get; }

    IAsyncRelayCommand UseSettingsCommand { get; }

    IAsyncRelayCommand ImageToImageCommand { get; }

    IAsyncRelayCommand ImageInfoCommand { get; }

    IAsyncRelayCommand SendToCanvasCommand { get; }

    IRelayCommand NextItemCommand { get; }

    IRelayCommand PreviousItemCommand { get; }
}
