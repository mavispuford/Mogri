using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.ViewModels.Pages;

namespace MobileDiffusion.Views;

public class BasePage : ContentPage
{
    public BasePage()
    {
        // For some reason, the back button behavior occasionally breaks when setting it from the XAML.  
        // https://github.com/dotnet/maui/issues/33139
        // Handling it here instead...
        var backButtonBehavior = new BackButtonBehavior();
        backButtonBehavior.SetBinding(BackButtonBehavior.CommandProperty, nameof(IPageViewModel.BackButtonCommand));
        Shell.SetBackButtonBehavior(this, backButtonBehavior);

        if (Application.Current != null &&
            Application.Current.Resources.TryGetValue("Primary", out var lightStatusBarColor) &&
            Application.Current.Resources.TryGetValue("Black", out var darkStatusBarColor))
        {
            var statusBarBehavior = new StatusBarBehavior()
            {
                StatusBarStyle = StatusBarStyle.LightContent
            };

            statusBarBehavior.SetAppThemeColor(StatusBarBehavior.StatusBarColorProperty, (Color)lightStatusBarColor, (Color)darkStatusBarColor);
            
            Behaviors.Add(statusBarBehavior);
        }
    }

    protected override void OnBindingContextChanged()
    {
        var backButtonBehavior = Shell.GetBackButtonBehavior(this);

        if (backButtonBehavior != null)
        {
            backButtonBehavior.BindingContext = BindingContext;
        }

        base.OnBindingContextChanged();
    }

    protected override async void OnAppearing()
    {
        try
        {
            base.OnAppearing();

            if (BindingContext is IPageViewModel pageViewModel)
            {
                await pageViewModel.OnAppearingAsync();
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
            base.OnAppearing();

            if (BindingContext is IPageViewModel pageViewModel)
            {
                await pageViewModel.OnDisappearingAsync();
            }
        }
        catch
        {
            // Specific exceptions should be handled in the VM, catching here to prevent app crashes in an async void method.
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