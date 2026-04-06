using System;
using CommunityToolkit.Mvvm.Input;

namespace Mogri.Interfaces.ViewModels.Pages;

public interface IAboutPageViewModel : IPageViewModel
{
    string AppVersion { get; }
    IAsyncRelayCommand CopyVersionToClipboardCommand { get; }
    IAsyncRelayCommand NavigateToGitHubPageCommand { get; }
    IAsyncRelayCommand NavigateToLicensesPageCommand { get; }
}
