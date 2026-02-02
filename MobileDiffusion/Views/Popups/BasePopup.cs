using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.ViewModels.Popups;
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

        if (Application.Current.Resources.TryGetValue("Gray950", out var statusBarColor))
        {
            Behaviors.Add(new StatusBarBehavior()
            {
                StatusBarColor = (Color)statusBarColor,
                StatusBarStyle = StatusBarStyle.LightContent
            });
        }

        this.SetBinding(BackgroundColorProperty, nameof(IPopupBaseViewModel.PopupBackgroundColor));
    }

    protected override async void OnAppearing()
    {
        try
        {
            base.OnAppearing();

            if (BindingContext is IPopupBaseViewModel viewModel)
            {
                await viewModel.OnAppearingAsync();
            }
        }
        catch
        {
            // Specific exceptions should be handled in the VM, catching here to prevent app crashes in an async void method.
        }
    }

    protected override async void OnDisappearing()
    {
        try
        {
            base.OnDisappearing();

            if (BindingContext is IPopupBaseViewModel viewModel)
            {
                await viewModel.OnDisappearingAsync();
            }
        }
        catch
        {
            // Specific exceptions should be handled in the VM, catching here to prevent app crashes in an async void method.
        }
    }

    protected override async void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        // NOTE - THIS DOES NOT SEEM TO GET CALLED

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
        // NOTE - THIS DOES NOT SEEM TO GET CALLED

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
