using CommunityToolkit.Maui.Animations;
using MobileDiffusion.Helpers;

namespace MobileDiffusion.Animations;

public class ColorToAnimation : BaseAnimation
{
    public static readonly BindableProperty ToColorProperty = BindableProperty.Create(nameof(ToColor), typeof(Color), typeof(ColorToAnimation), Colors.Transparent);
    public Color ToColor 
    { 
        get => (Color)GetValue(ToColorProperty);
        set => SetValue(ToColorProperty, value);
    }

    public static readonly BindableProperty TargetPropertyProperty = BindableProperty.Create(nameof(TargetProperty), typeof(BindableProperty), typeof(ColorToAnimation), null);
    public BindableProperty TargetProperty 
    { 
        get => (BindableProperty)GetValue(TargetPropertyProperty);
        set => SetValue(TargetPropertyProperty, value);
    }

    public override Task Animate(VisualElement view, CancellationToken token = default)
    {
        if (view == null || TargetProperty == null)
        {
            return Task.CompletedTask;
        }

        string animationName = $"ColorTo_{TargetProperty.PropertyName}";
        
        // Abort any existing animation on this property first
        view.AbortAnimation(animationName);

        var fromColor = (Color)view.GetValue(TargetProperty) ?? Colors.Transparent;

        return view.ColorTo(fromColor, ToColor, color => view.SetValue(TargetProperty, color), (uint)Length, Easing, animationName);
    }
}
