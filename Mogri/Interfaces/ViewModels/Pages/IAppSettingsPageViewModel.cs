using CommunityToolkit.Mvvm.Input;

namespace Mogri.Interfaces.ViewModels.Pages;

internal interface IAppSettingsPageViewModel : IPageViewModel
{
    string ServerUrl { get; set; }

    IReadOnlyList<string> AvailableBackends { get; }

    string SelectedBackend { get; set; }

    bool IsComfyCloudSelected { get; }
    
    bool IsServerUrlVisible { get; }

    string ComfyCloudApiKey { get; set; }
    
    string CustomAuthHeaderName { get; set; }
    
    string CustomAuthHeaderValue { get; set; }

    IAsyncRelayCommand ResetValuesCommand { get; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmSettingsCommand { get; }
}
