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

    protected override async void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        try
        {
            base.OnNavigatedFrom(args);

            if (BindingContext is IPageViewModel pageViewModel)
            {
                await pageViewModel.OnNavigatedFromAsync();
            }
        }
        catch
        {
            // Specific exceptions should be handled in the VM, catching here to prevent app crashes in an async void method.
        }
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        try
        {
            base.OnNavigatedTo(args);

            if (BindingContext is IPageViewModel pageViewModel)
            {
                await pageViewModel.OnNavigatedToAsync();
            }
        }
        catch
        {
            // Specific exceptions should be handled in the VM, catching here to prevent app crashes in an async void method.
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