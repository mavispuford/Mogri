using MobileDiffusion.Interfaces.ViewModels;
using Mopups.Pages;

namespace MobileDiffusion.Views.Popups;

public class BasePopup : PopupPage
{
    public BasePopup() : base()
    {
        // Global styles don't seem to be working with Popups, so we'll set it up here

        if (!Application.Current.Resources.TryGetValue("BlackSeventyThreePercent", out var bgColor))
        {
            bgColor = Color.FromArgb("BB000000");
        }

        BackgroundColor = (Color)bgColor;

        CloseWhenBackgroundIsClicked = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is IPopupBaseViewModel viewModel)
        {
            viewModel.OnAppearing();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (BindingContext is IPopupBaseViewModel viewModel)
        {
            viewModel.OnDisappearing();
        }
    }

    protected override async void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        try
        {
            base.OnNavigatedFrom(args);

            if (BindingContext is IPopupBaseViewModel viewModel)
            {
                await viewModel.OnNavigatedFromAsync();
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

            if (BindingContext is IPopupBaseViewModel viewModel)
            {
                await viewModel.OnNavigatedToAsync();
            }
        }
        catch
        {
            // Specific exceptions should be handled in the VM, catching here to prevent app crashes in an async void method.
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is IPopupBaseViewModel viewModel)
        {
            viewModel.OnBackButtonPressed();
            
            return true;
        }
        else
        {
            return base.OnBackButtonPressed();
        }
    }
}
