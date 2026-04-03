using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;

namespace Mogri.Views;

public class BasePage : ContentPage
{
    public static readonly BindableProperty LightStatusBarColorProperty =
        BindableProperty.Create(nameof(LightStatusBarColor), typeof(Color), typeof(BasePage), Colors.Transparent, propertyChanged: OnStatusBarColorChanged);

    public static readonly BindableProperty DarkStatusBarColorProperty =
        BindableProperty.Create(nameof(DarkStatusBarColor), typeof(Color), typeof(BasePage), Colors.Transparent, propertyChanged: OnStatusBarColorChanged);

    public static readonly BindableProperty StatusBarStyleProperty =
        BindableProperty.Create(nameof(StatusBarStyle), typeof(StatusBarStyle), typeof(BasePage), StatusBarStyle.LightContent, propertyChanged: OnStatusBarStyleChanged);

    public Color LightStatusBarColor
    {
        get => (Color)GetValue(LightStatusBarColorProperty);
        set => SetValue(LightStatusBarColorProperty, value);
    }

    public Color DarkStatusBarColor
    {
        get => (Color)GetValue(DarkStatusBarColorProperty);
        set => SetValue(DarkStatusBarColorProperty, value);
    }

    public StatusBarStyle StatusBarStyle
    {
        get => (StatusBarStyle)GetValue(StatusBarStyleProperty);
        set => SetValue(StatusBarStyleProperty, value);
    }

    private readonly StatusBarBehavior? _statusBarBehavior;

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
            Application.Current.Resources.TryGetValue("CadetDark", out var darkStatusBarColor))
        {
            LightStatusBarColor = (Color)lightStatusBarColor;
            DarkStatusBarColor = (Color)darkStatusBarColor;

            // CA1416: StatusBarBehavior is supported on iOS and Android, but the analyzer incorrectly flags
            // macCatalyst (which this app does not target) as an unsupported reachable platform within the iOS TFM.
#pragma warning disable CA1416
            _statusBarBehavior = new StatusBarBehavior()
            {
                StatusBarStyle = this.StatusBarStyle,
                ApplyOn = StatusBarApplyOn.OnBehaviorAttachedTo
            };

            _statusBarBehavior.SetAppThemeColor(StatusBarBehavior.StatusBarColorProperty, LightStatusBarColor, DarkStatusBarColor);
#pragma warning restore CA1416

            Behaviors.Add(_statusBarBehavior);
        }
    }

    private static void OnStatusBarStyleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BasePage page && page._statusBarBehavior != null)
        {
#pragma warning disable CA1416
            page._statusBarBehavior.StatusBarStyle = page.StatusBarStyle;
#pragma warning restore CA1416
        }
    }

    private static void OnStatusBarColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BasePage page && page._statusBarBehavior != null)
        {
#pragma warning disable CA1416
            page._statusBarBehavior.SetAppThemeColor(StatusBarBehavior.StatusBarColorProperty, page.LightStatusBarColor, page.DarkStatusBarColor);
            
            if (Application.Current != null)
            {
                page._statusBarBehavior.StatusBarColor = Application.Current.RequestedTheme == AppTheme.Dark ? page.DarkStatusBarColor : page.LightStatusBarColor;
            }
#pragma warning restore CA1416
        }
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        if (_statusBarBehavior != null)
        {
#pragma warning disable CA1416
            _statusBarBehavior.StatusBarStyle = StatusBarStyle;
            _statusBarBehavior.StatusBarColor = e.RequestedTheme == AppTheme.Dark ? DarkStatusBarColor : LightStatusBarColor;
#pragma warning restore CA1416
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

            if (Application.Current != null)
            {
                Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
            }

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
            base.OnDisappearing();

            if (Application.Current != null)
            {
                Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
            }

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
