using CommunityToolkit.Maui.Animations;

namespace MobileDiffusion.Animations;

public class ScaleToAnimation : BaseAnimation
{
    public static readonly BindableProperty ScaleProperty = BindableProperty.Create(nameof(Scale), typeof(double), typeof(ScaleToAnimation), 1.0);

    public double Scale 
    { 
        get => (double)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public override Task Animate(VisualElement view, CancellationToken token = default) => view.ScaleToAsync(Scale, Length, Easing);
}
