namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPopupBaseViewModel : IQueryAttributable, IBaseViewModel
{
    void OnAppearing();

    void OnDisappearing();

    Task OnNavigatedFromAsync();

    Task OnNavigatedToAsync();

    void OnBackButtonPressed();
}
