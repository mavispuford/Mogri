using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels.Pages;

public interface IPageViewModel : IQueryAttributable, IBaseViewModel
{
    IAsyncRelayCommand BackButtonCommand { get; }

    Task OnAppearingAsync();

    Task OnDisappearingAsync();

    Task OnNavigatedFromAsync();

    Task OnNavigatedToAsync();

    bool OnBackButtonPressed();
}
