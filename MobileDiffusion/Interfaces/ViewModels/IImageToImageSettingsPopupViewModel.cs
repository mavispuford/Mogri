using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IImageToImageSettingsPopupViewModel : IPopupBaseViewModel
{
    string Strength { get; set; }

    string StrengthPlaceholder { get; set; }

    ImageSource InitImageSource { get; set; }

    IAsyncRelayCommand ResetValuesCommand { get; }

    IRelayCommand CancelCommand { get; }

    IRelayCommand ConfirmSettingsCommand { get; }

    IAsyncRelayCommand ShowMediaPickerCommand { get; }
}
