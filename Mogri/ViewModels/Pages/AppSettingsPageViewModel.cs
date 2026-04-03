using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;

namespace Mogri.ViewModels;

internal partial class AppSettingsPageViewModel : PageViewModel, IAppSettingsPageViewModel
{
    private readonly IBackendRegistry _backendRegistry;
    private readonly IPopupService _popupService;

    [ObservableProperty]
    public partial string ServerUrl { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> AvailableBackends { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComfyCloudSelected))]
    [NotifyPropertyChangedFor(nameof(IsServerUrlVisible))]
    public partial string SelectedBackend { get; set; }

    [ObservableProperty]
    public partial string ComfyCloudApiKey { get; set; }

    [ObservableProperty]
    public partial string CustomAuthHeaderName { get; set; }

    [ObservableProperty]
    public partial string CustomAuthHeaderValue { get; set; }

    public bool IsComfyCloudSelected => SelectedBackend == "Comfy Cloud";
    public bool IsServerUrlVisible => SelectedBackend != "Comfy Cloud";

    public AppSettingsPageViewModel(
        ILoadingService loadingService,
        IBackendRegistry backendRegistry,
        IPopupService popupService) : base(loadingService)
    {
        _backendRegistry = backendRegistry;
        _popupService = popupService;
        
        ServerUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);
        ComfyCloudApiKey = Preferences.Default.Get(Constants.PreferenceKeys.ComfyCloudApiKey, string.Empty);
        CustomAuthHeaderName = Preferences.Default.Get(Constants.PreferenceKeys.AuthHeaderName, string.Empty);
        CustomAuthHeaderValue = Preferences.Default.Get(Constants.PreferenceKeys.AuthHeaderValue, string.Empty);

        AvailableBackends = _backendRegistry.GetAllBackends().Select(b => b.Name).ToList();
        SelectedBackend = Preferences.Default.Get(Constants.PreferenceKeys.SelectedBackend, AvailableBackends.FirstOrDefault() ?? "SD Forge Neo");
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ConfirmSettings()
    {
        // Allow empty URL
        Preferences.Default.Set(Constants.PreferenceKeys.ServerUrl, ServerUrl);

        if (!IsComfyCloudSelected)
        {
            ComfyCloudApiKey = string.Empty;
        }

        if (!IsServerUrlVisible)
        {
            CustomAuthHeaderName = string.Empty;
            CustomAuthHeaderValue = string.Empty;
        }

        // Allow empty ComfyCloudApiKey
        Preferences.Default.Set(Constants.PreferenceKeys.ComfyCloudApiKey, ComfyCloudApiKey);
        Preferences.Default.Set(Constants.PreferenceKeys.AuthHeaderName, CustomAuthHeaderName);
        Preferences.Default.Set(Constants.PreferenceKeys.AuthHeaderValue, CustomAuthHeaderValue);

        if (!string.IsNullOrEmpty(SelectedBackend))
        {
            Preferences.Default.Set(Constants.PreferenceKeys.SelectedBackend, SelectedBackend);
        }

        var parameters = new Dictionary<string, object> { { NavigationParams.ForceReinitialize, true } };

        await Shell.Current.GoToAsync("..", parameters);
    }

    [RelayCommand]
    private async Task ResetValues()
    {
        var result = await _popupService.DisplayAlertAsync("Confirm Reset", "Are you sure you would like to reset back to defaults?", "RESET", "Cancel");

        if (!result)
        {
            return;
        }

        ServerUrl = string.Empty;
        ComfyCloudApiKey = string.Empty;
        CustomAuthHeaderName = string.Empty;
        CustomAuthHeaderValue = string.Empty;

        SelectedBackend = AvailableBackends.FirstOrDefault() ?? "SD Forge Neo";
    }
}
