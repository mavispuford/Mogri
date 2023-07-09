using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Views;

public class BasePage : ContentPage
{
    public BasePage()
    {
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is IPageViewModel pageViewModel)
        {
            pageViewModel.OnAppearing();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (BindingContext is IPageViewModel pageViewModel)
        {
            pageViewModel.OnDisappearing();
        }
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        
        if (BindingContext is IPageViewModel pageViewModel)
        {
            pageViewModel.OnNavigatedFrom();
        }
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (BindingContext is IPageViewModel pageViewModel)
        {
            pageViewModel.OnNavigatedTo();
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is IPageViewModel pageViewModel)
        {
            var result = pageViewModel.OnBackButtonPressed();

            if (result)
            {
                return true;
            }
        }

        return base.OnBackButtonPressed();
    }
}