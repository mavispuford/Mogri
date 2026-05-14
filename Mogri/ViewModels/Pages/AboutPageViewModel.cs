using System;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels.Pages;

namespace Mogri.ViewModels.Pages;

public partial class AboutPageViewModel : PageViewModel, IAboutPageViewModel
{
    private const string GitHubPageUrl = "https://github.com/mavispuford/Mogri";
    private readonly IToastService _toastService;

    public AboutPageViewModel(
        ILoadingCoordinator loadingCoordinator,
        IToastService toastService,
        INavigationService navigationService) : base(loadingCoordinator, navigationService)
    {
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
    }

    public string AppVersion => $"v{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

    [RelayCommand]
    private async Task CopyVersionToClipboard()
    {
        await Clipboard.Default.SetTextAsync(AppVersion);
        await _toastService.ShowAsync("Copied to clipboard");
    }

    [RelayCommand]
    private Task NavigateToGitHubPage()
    {
        return Browser.Default.OpenAsync(GitHubPageUrl);
    }

    [RelayCommand]
    private Task NavigateToLicensesPage()
    {
        return NavigationService.GoToAsync("LicensesPage");
    }

}