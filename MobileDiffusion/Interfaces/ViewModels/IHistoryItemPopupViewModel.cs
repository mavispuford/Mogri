using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IHistoryItemPopupViewModel : IPopupBaseViewModel
{
    IHistoryItemViewModel HistoryItem { get; set; }

    ImageSource FullImageSource { get; set; }

    IAsyncRelayCommand DeleteCommand { get; }

    IRelayCommand CloseCommand { get; }

    IAsyncRelayCommand SaveCommand { get; }

    IRelayCommand UseSettingsCommand { get; }

    IAsyncRelayCommand ImageToImageCommand { get; }

    IAsyncRelayCommand ImageInfoCommand { get; }

    IAsyncRelayCommand SendToCanvasCommand { get; }
}
