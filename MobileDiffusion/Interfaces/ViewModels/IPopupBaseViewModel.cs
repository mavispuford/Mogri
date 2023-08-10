namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPopupBaseViewModel : IQueryAttributable, IBaseViewModel
{
    Task OnAppearingAsync();

    Task OnDisappearingAsync();

    Task OnNavigatedFromAsync();

    Task OnNavigatedToAsync();

    void OnBackButtonPressed();
}
