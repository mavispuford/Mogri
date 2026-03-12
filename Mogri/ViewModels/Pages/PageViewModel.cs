using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;

namespace Mogri.ViewModels;

public partial class PageViewModel : BaseViewModel, IPageViewModel
{
    protected ILoadingService LoadingService { get; set; }

    public PageViewModel(ILoadingService loadingService)
    {
        LoadingService = loadingService ?? throw new ArgumentNullException(nameof(loadingService));
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
        await Shell.Current.GoToAsync("..");
    }
}
