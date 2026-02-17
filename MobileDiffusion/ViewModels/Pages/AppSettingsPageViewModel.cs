using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.ViewModels.Pages;

namespace MobileDiffusion.ViewModels;

internal partial class AppSettingsPageViewModel : PageViewModel, IAppSettingsPageViewModel
{
    private readonly IBackendRegistry _backendRegistry;

    [ObservableProperty]
    public partial string ServerUrl { get; set; }

    [ObservableProperty]
    public partial string DefaultWidth { get; set; }

    [ObservableProperty]
    public partial string DefaultHeight { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> AvailableBackends { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComfyUiSelected))]
    public partial string SelectedBackend { get; set; }

    [ObservableProperty]
    public partial string ComfyUiApiKey { get; set; }

    public bool IsComfyUiSelected => SelectedBackend == "ComfyUI";

    public AppSettingsPageViewModel(
        ILoadingService loadingService,
        IBackendRegistry backendRegistry) : base(loadingService)
    {
        _backendRegistry = backendRegistry;
        
        ServerUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);
        ComfyUiApiKey = Preferences.Default.Get(Constants.PreferenceKeys.ComfyUiApiKey, string.Empty);
        DefaultWidth = Preferences.Default.Get<double>(Constants.PreferenceKeys.DefaultWidth, 512).ToString();
        DefaultHeight = Preferences.Default.Get<double>(Constants.PreferenceKeys.DefaultHeight, 512).ToString();

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
        if (!string.IsNullOrEmpty(ServerUrl))
        {
            Preferences.Default.Set(Constants.PreferenceKeys.ServerUrl, ServerUrl);
        }

        if (!string.IsNullOrEmpty(ComfyUiApiKey))
        {
            Preferences.Default.Set(Constants.PreferenceKeys.ComfyUiApiKey, ComfyUiApiKey);
        }

        if (!string.IsNullOrEmpty(DefaultWidth) && double.TryParse(DefaultWidth, out double defaultWidth))
        {
            Preferences.Default.Set(Constants.PreferenceKeys.DefaultWidth, defaultWidth);
        }

        if (!string.IsNullOrEmpty(DefaultHeight) && double.TryParse(DefaultHeight, out double defaultHeight))
        {
            Preferences.Default.Set(Constants.PreferenceKeys.DefaultHeight, defaultHeight);
        }

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
        var result = await Shell.Current.DisplayAlertAsync("Confirm Reset", "Are you sure you would like to reset back to defaults?", "RESET", "Cancel");

        if (!result)
        {
            return;
        }

        ServerUrl = string.Empty;
        ComfyUiApiKey = string.Empty;
        DefaultWidth = "512";
        DefaultHeight = "512";

        SelectedBackend = AvailableBackends.FirstOrDefault() ?? "SD Forge Neo";
    }
}
