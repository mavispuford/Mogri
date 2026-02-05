#nullable enable
using CommunityToolkit.Maui.Behaviors;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace MobileDiffusion.Behaviors;

/// <summary>
///     A behavior that wraps <see cref="IconTintColorBehavior"/> to support <see cref="AppThemeBinding"/>-like functionality.
/// </summary>
/// <remarks>
/// Standard behaviors do not support <see cref="AppThemeBinding"/> reliably because they are not part of the Visual Tree 
/// and do not receive theme change notifications. This class manually monitors <see cref="Application.RequestedThemeChanged"/> 
/// and applies the appropriate color.
/// <para>
/// Usage:
/// &lt;behaviors:ThemeAwareIconTintColorBehavior Light="{StaticResource Primary}" Dark="{StaticResource White}" /&gt;
/// </para>
/// <para>
/// If only one property is set, it will be used as a fallback for both themes.
/// </para>
/// </remarks>
public class ThemeAwareIconTintColorBehavior : Behavior<View>
{
    private readonly IconTintColorBehavior _wrappedBehavior = new();

    public static readonly BindableProperty LightProperty =
        BindableProperty.Create(nameof(Light), typeof(Color), typeof(ThemeAwareIconTintColorBehavior), null, propertyChanged: OnThemeColorChanged);

    /// <summary>
    /// The color to apply when the application is in Light theme.
    /// If <see cref="Dark"/> is not set, this color will also be used for Dark theme.
    /// </summary>
    public Color Light
    {
        get => (Color)GetValue(LightProperty);
        set => SetValue(LightProperty, value);
    }

    public static readonly BindableProperty DarkProperty =
        BindableProperty.Create(nameof(Dark), typeof(Color), typeof(ThemeAwareIconTintColorBehavior), null, propertyChanged: OnThemeColorChanged);

    /// <summary>
    /// The color to apply when the application is in Dark theme.
    /// If <see cref="Light"/> is not set, this color will also be used for Light theme.
    /// </summary>
    public Color Dark
    {
        get => (Color)GetValue(DarkProperty);
        set => SetValue(DarkProperty, value);
    }

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);

        // Add the wrapped behavior to the element
        bindable.Behaviors.Add(_wrappedBehavior);

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        }
        ApplyTheme();
    }

    protected override void OnDetachingFrom(View bindable)
    {
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        }

        // Clean up
        bindable.Behaviors.Remove(_wrappedBehavior);

        base.OnDetachingFrom(bindable);
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        ApplyTheme();
    }

    private static void OnThemeColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ThemeAwareIconTintColorBehavior behavior)
        {
            behavior.ApplyTheme();
        }
    }

    private void ApplyTheme()
    {
        if (Application.Current == null) return;

        var theme = Application.Current.RequestedTheme;

        Color? colorToApply = null;

        if (theme == AppTheme.Dark)
        {
            // Use Dark if available, otherwise fallback to Light
            colorToApply = Dark ?? Light;
        }
        else
        {
            // Use Light if available, otherwise fallback to Dark
            colorToApply = Light ?? Dark;
        }

        if (colorToApply != null)
        {
            _wrappedBehavior.TintColor = colorToApply;
        }
    }
}
