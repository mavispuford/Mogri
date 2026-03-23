using System;
using CommunityToolkit.Mvvm.Input;

namespace Mogri.Interfaces.ViewModels.Pages;

public interface IAboutPageViewModel : IPageViewModel
{
    IAsyncRelayCommand NavigateToGitHubPageCommand { get; }
    IAsyncRelayCommand NavigateToLicensesPageCommand { get; }
}
