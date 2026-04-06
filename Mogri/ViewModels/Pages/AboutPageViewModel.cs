using System;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels.Pages;

namespace Mogri.ViewModels.Pages;

public partial class AboutPageViewModel : PageViewModel, IAboutPageViewModel
{
    private const string GitHubPageUrl = "https://github.com/mavispuford/Mogri";

    public AboutPageViewModel(
        ILoadingService loadingService) : base(loadingService)
    {
    }

    public string AppVersion => $"v{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

    [RelayCommand]
    private async Task CopyVersionToClipboard()
    {
        await Clipboard.Default.SetTextAsync(AppVersion);
        await Toast.Make("Copied to clipboard").Show();
    }

    [RelayCommand]
    private Task NavigateToGitHubPage()
    {
        return Browser.Default.OpenAsync(GitHubPageUrl);
    }

    [RelayCommand]
    private Task NavigateToLicensesPage()
    {
        return Shell.Current.GoToAsync("LicensesPage");
    }
}
