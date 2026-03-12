using CommunityToolkit.Mvvm.Input;

namespace Mogri.Interfaces.ViewModels.Pages;

public interface IImageToImageSettingsPageViewModel : IPageViewModel
{
    bool FitImageServerSide { get; set; }

    bool FitImageClientSide { get; set; }

    bool IsLoadingInitImage { get; set; }

    bool IsLoadingMaskImage { get; set; }

    string? Strength { get; set; }

    string? StrengthPlaceholder { get; set; }

    ImageSource? InitImageSource { get; set; }

    ImageSource? MaskImageSource { get; set; }

    IAsyncRelayCommand ResetValuesCommand { get; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmSettingsCommand { get; }

    IAsyncRelayCommand<bool> ShowMediaPickerCommand { get; }
}
