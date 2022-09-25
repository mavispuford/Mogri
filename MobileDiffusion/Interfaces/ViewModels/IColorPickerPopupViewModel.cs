using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IColorPickerPopupViewModel : IPopupBaseViewModel
{
    Color CurrentColor { get; set; }

    string CurrentColorHexString { get; set; }

    List<Color> Swatches { get; set; }

    List<Color> SwatchesFromImage { get; set; }

    IRelayCommand CancelCommand { get; }

    IRelayCommand ConfirmCommand { get; }

    IRelayCommand<Color> SwatchTappedCommand { get; }
}
