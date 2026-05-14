using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;

namespace Mogri.ViewModels;

public partial class PageViewModel : BaseViewModel, IPageViewModel
{
    protected ILoadingCoordinator LoadingCoordinator { get; }
    protected INavigationService NavigationService { get; }

    public PageViewModel(ILoadingCoordinator loadingCoordinator, INavigationService navigationService)
    {
        LoadingCoordinator = loadingCoordinator ?? throw new ArgumentNullException(nameof(loadingCoordinator));
        NavigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
    }

    public virtual void ApplyQueryAttributes(IDictionary<string, object> query)
    {
    }

    public virtual Task OnAppearingAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnDisappearingAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnNavigatedFromAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnNavigatedToAsync()
    {
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task BackButton()
    {
        OnBackButtonPressed();
    }

    public virtual bool OnBackButtonPressed()
    {
        // Executing the command to avoid using async void
        NavigateBackCommand.Execute(null);

        return false;
    }

    [RelayCommand]
    protected async Task NavigateBack()
    {
        await NavigationService.GoBackAsync();
    }

}