using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IImageToImageSettingsPageViewModel : IPageViewModel
{
    string Strength { get; set; }

    string StrengthPlaceholder { get; set; }

    ImageSource InitImageSource { get; set; }

    IAsyncRelayCommand ResetValuesCommand { get; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmSettingsCommand { get; }

    IAsyncRelayCommand ShowMediaPickerCommand { get; }
}
