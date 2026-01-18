using CommunityToolkit.Maui.Animations;

namespace MobileDiffusion.Animations;

public class FadeToAnimation : BaseAnimation
{
    public static readonly BindableProperty OpacityProperty = BindableProperty.Create(nameof(Opacity), typeof(double), typeof(FadeToAnimation), 1.0);

    public double Opacity 
    { 
        get => (double)GetValue(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    public override Task Animate(VisualElement view, CancellationToken token = default) => view.FadeToAsync(Opacity, Length, Easing);
}
