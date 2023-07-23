namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPageViewModel : IQueryAttributable, IBaseViewModel
{
    void OnAppearing();

    void OnDisappearing();

    Task OnNavigatedFromAsync();
    
    Task OnNavigatedToAsync();

    bool OnBackButtonPressed();
}
