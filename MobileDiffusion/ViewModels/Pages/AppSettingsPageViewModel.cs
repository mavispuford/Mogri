using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

internal partial class AppSettingsPageViewModel : PageViewModel, IAppSettingsPageViewModel
{
    [ObservableProperty]
    private string serverUrl;

    [ObservableProperty]
    private string _defaultWidth;
    
    [ObservableProperty]
    private string _defaultHeight;

    public AppSettingsPageViewModel(ILoadingService loadingService) : base(loadingService)
    {
        ServerUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);
        DefaultWidth = Preferences.Default.Get<double>(Constants.PreferenceKeys.DefaultWidth, 512).ToString();
        DefaultHeight = Preferences.Default.Get<double>(Constants.PreferenceKeys.DefaultHeight, 512).ToString();
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

        if (!string.IsNullOrEmpty(DefaultWidth) && double.TryParse(DefaultWidth, out double defaultWidth))
        {
            Preferences.Default.Set(Constants.PreferenceKeys.DefaultWidth, defaultWidth);
        }

        if (!string.IsNullOrEmpty(DefaultHeight) && double.TryParse(DefaultHeight, out double defaultHeight))
        {
            Preferences.Default.Set(Constants.PreferenceKeys.DefaultHeight, defaultHeight);
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ResetValues()
    {
        var result = await Shell.Current.DisplayAlert("Confirm Reset", "Are you sure you would like to reset back to defaults?", "RESET", "Cancel");

        if (!result)
        {
            return;
        }

        ServerUrl = string.Empty;
        DefaultWidth = "512";
        DefaultHeight = "512";
    }
}
