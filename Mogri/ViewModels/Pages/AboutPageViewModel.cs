using System;
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
