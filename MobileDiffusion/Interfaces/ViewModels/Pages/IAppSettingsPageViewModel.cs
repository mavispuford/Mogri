using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels.Pages;

internal interface IAppSettingsPageViewModel : IPageViewModel
{
    string ServerUrl { get; set; }

    IReadOnlyList<string> AvailableBackends { get; }

    string SelectedBackend { get; set; }

    IAsyncRelayCommand ResetValuesCommand { get; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmSettingsCommand { get; }
}
