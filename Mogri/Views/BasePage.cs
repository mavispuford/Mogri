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

    public static readonly BindableProperty LightToolBarColorProperty =
        BindableProperty.Create(nameof(LightToolBarColor), typeof(Color), typeof(BasePage), Colors.Transparent, propertyChanged: OnToolBarColorChanged);

    public static readonly BindableProperty DarkToolBarColorProperty =
        BindableProperty.Create(nameof(DarkToolBarColor), typeof(Color), typeof(BasePage), Colors.Transparent, propertyChanged: OnToolBarColorChanged);

    public Color LightToolBarColor
    {
        get => (Color)GetValue(LightToolBarColorProperty);
        set => SetValue(LightToolBarColorProperty, value);
    }

    public Color DarkToolBarColor
    {
        get => (Color)GetValue(DarkToolBarColorProperty);
        set => SetValue(DarkToolBarColorProperty, value);
    }

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
            Application.Current.Resources.TryGetValue("Primary", out var lightColor) &&
            Application.Current.Resources.TryGetValue("CadetDark", out var darkColor))
        {
            LightToolBarColor = (Color)lightColor;
            DarkToolBarColor = (Color)darkColor;

            LightStatusBarColor = (Color)lightColor;
            DarkStatusBarColor = (Color)darkColor;
        }

        // Let MAUI's binding engine keep the toolbar color in sync with the theme automatically,
        // avoiding a flash of stale color when switching tabs after a theme change.
        this.SetAppThemeColor(Shell.BackgroundColorProperty, LightToolBarColor, DarkToolBarColor);

        // CA1416: StatusBarBehavior is supported on iOS and Android, but the analyzer incorrectly flags
        // macCatalyst (which this app does not target) as an unsupported reachable platform within the iOS TFM.
#pragma warning disable CA1416
        _statusBarBehavior = new StatusBarBehavior()
        {
            StatusBarStyle = StatusBarStyle,
            ApplyOn = StatusBarApplyOn.OnBehaviorAttachedTo // Start on AttachedTo to apply instantly, then switch in OnDisappearing to ensure that navigating back will also set the right colors
        };

        _statusBarBehavior.SetAppThemeColor(StatusBarBehavior.StatusBarColorProperty, LightStatusBarColor, DarkStatusBarColor);
#pragma warning restore CA1416

        Behaviors.Add(_statusBarBehavior);
    }

    private static void OnStatusBarStyleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BasePage page && Application.Current != null)
        {
            page.updateStatusBarBehavior(Application.Current.RequestedTheme);
        }
    }

    private static void OnStatusBarColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BasePage page && Application.Current != null)
        {
            page.updateStatusBarBehavior(Application.Current.RequestedTheme);
        }
    }

    private static void OnToolBarColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BasePage page)
        {
            page.updateToolBarColor();
        }
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        updateToolBarColor();
        updateStatusBarBehavior(e.RequestedTheme);
    }

    private void updateToolBarColor()
    {
        if (Application.Current != null)
        {
            var toolBarColor = Application.Current.RequestedTheme == AppTheme.Light ? LightToolBarColor : DarkToolBarColor;

            // Manually set the color because in some cases, each platform doesn't update with just AppThemeBinding
            Shell.SetBackgroundColor(this, toolBarColor);
        }

        // Skipping AppThemeBinding because it doesn't work when a modal is shown and light/dark theme changes, then the modal is popped off the stack
        // this.SetAppThemeColor(Shell.BackgroundColorProperty, LightToolBarColor, DarkToolBarColor);
    }

    private void updateStatusBarBehavior(AppTheme appTheme)
    {
        if (_statusBarBehavior != null)
        {
#pragma warning disable CA1416
            _statusBarBehavior.StatusBarStyle = StatusBarStyle;
            _statusBarBehavior.StatusBarColor = appTheme == AppTheme.Dark ? DarkStatusBarColor : LightStatusBarColor;
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
                updateToolBarColor();
                updateStatusBarBehavior(Application.Current.RequestedTheme);
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

            if (_statusBarBehavior != null)
            {
                // This ensures that navigating back to the page will trigger status bar color changes
#pragma warning disable CA1416
                _statusBarBehavior.ApplyOn = StatusBarApplyOn.OnPageNavigatedTo;
#pragma warning restore CA1416
            }

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
