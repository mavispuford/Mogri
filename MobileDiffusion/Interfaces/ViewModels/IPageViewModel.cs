namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPageViewModel : IQueryAttributable, IBaseViewModel
{
    void OnAppearing();

    void OnDisappearing();

    void OnNavigatedFrom();
    
    void OnNavigatedTo();

    bool OnBackButtonPressed();
}
