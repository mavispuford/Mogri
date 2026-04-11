using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;
using Mopups.Pages;

namespace Mogri.Views.Popups;

public class BasePopup : PopupPage
{
    public BasePopup() : base()
    {
        // Global styles don't seem to be working with Popups, so we'll set it up here

        object bgColor = Color.FromArgb("BB000000");

        if (Application.Current != null && Application.Current.Resources.TryGetValue("BlackSeventyThreePercent", out var foundColor))
        {
            bgColor = foundColor;
        }

        BackgroundColor = (Color)bgColor;

        CloseWhenBackgroundIsClicked = false;

        if (Application.Current != null &&
            Application.Current.Resources.TryGetValue("Primary", out var lightStatusBarColor) &&
            Application.Current.Resources.TryGetValue("Black", out var darkStatusBarColor))
        {
            // CA1416: StatusBarBehavior is supported on iOS and Android, but the analyzer incorrectly flags
            // macCatalyst (which this app does not target) as an unsupported reachable platform within the iOS TFM.
#pragma warning disable CA1416
            var statusBarBehavior = new StatusBarBehavior()
            {
                StatusBarStyle = StatusBarStyle.LightContent,
                ApplyOn = StatusBarApplyOn.OnPageNavigatedTo
            };

            statusBarBehavior.SetAppThemeColor(StatusBarBehavior.StatusBarColorProperty, (Color)lightStatusBarColor, (Color)darkStatusBarColor);
#pragma warning restore CA1416

            Behaviors.Add(statusBarBehavior);
        }

#if DEBUG
        MemoryToolkit.Maui.LeakMonitorBehavior.SetCascade(this, true);
#endif
        MemoryToolkit.Maui.TearDownBehavior.SetCascade(this, true); // Seems to prevent the LoadingPopup from showing

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
