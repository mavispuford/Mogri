namespace Mogri.Interfaces.ViewModels.Popups;

public interface IPopupBaseViewModel : IQueryAttributable, IBaseViewModel
{
    Task OnAppearingAsync();

    Task OnDisappearingAsync();

    Task OnNavigatedFromAsync();

    Task OnNavigatedToAsync();

    void OnBackButtonPressed();

    double ContentOpacity { get; }

    Color PopupBackgroundColor { get; }
}
