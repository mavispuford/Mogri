using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels.Popups;

public interface IColorPickerPopupViewModel : IPopupBaseViewModel
{
    Color CurrentColor { get; set; }

    string CurrentColorHexString { get; set; }

    List<Color> Swatches { get; set; }

    List<Color> SwatchesFromImage { get; set; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmCommand { get; }

    IRelayCommand<Color> SwatchTappedCommand { get; }
}
