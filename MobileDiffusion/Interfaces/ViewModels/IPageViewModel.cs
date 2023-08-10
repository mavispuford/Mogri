namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPageViewModel : IQueryAttributable, IBaseViewModel
{
    Task OnAppearingAsync();

    Task OnDisappearingAsync();

    Task OnNavigatedFromAsync();
    
    Task OnNavigatedToAsync();

    bool OnBackButtonPressed();
}
