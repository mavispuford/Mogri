using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels.Pages;

internal interface IAppSettingsPageViewModel : IPageViewModel
{
    string ServerUrl { get; set; }

    string DefaultWidth { get; set; }

    string DefaultHeight { get; set; }

    IAsyncRelayCommand ResetValuesCommand { get; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmSettingsCommand { get; }
}
