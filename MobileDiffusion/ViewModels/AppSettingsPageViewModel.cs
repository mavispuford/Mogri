using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

internal partial class AppSettingsPageViewModel : PageViewModel, IAppSettingsPageViewModel
{
    [ObservableProperty]
    private string serverUrl;

    public AppSettingsPageViewModel()
    {
        ServerUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ConfirmSettings()
    {
        // TODO - Save settings

        if (!string.IsNullOrEmpty(ServerUrl))
        {
            Preferences.Default.Set(Constants.PreferenceKeys.ServerUrl, ServerUrl);
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
    }
}
