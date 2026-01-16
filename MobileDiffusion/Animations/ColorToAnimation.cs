using CommunityToolkit.Maui.Animations;
using MobileDiffusion.Helpers;

namespace MobileDiffusion.Animations;

public class ColorToAnimation : BaseAnimation
{
    public Color ToColor { get; set; }

    public BindableProperty BindableProperty { get; set; }

    public override Task Animate(VisualElement view, CancellationToken token = default)
    {
        if (view == null || BindableProperty == null)
        {
            return Task.CompletedTask;
        }

        string animationName = $"ColorTo_{BindableProperty.PropertyName}";
        
        // Abort any existing animation on this property first
        view.AbortAnimation(animationName);

        var fromColor = (Color)view.GetValue(BindableProperty) ?? Colors.Transparent;

        return view.ColorTo(fromColor, ToColor, color => view.SetValue(BindableProperty, color), (uint)Length, Easing, animationName);
    }
}
