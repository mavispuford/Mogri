using CommunityToolkit.Maui.Animations;

namespace Mogri.Animations;

public class ScaleToAnimation : BaseAnimation
{
    public static readonly BindableProperty ScaleProperty = BindableProperty.Create(nameof(Scale), typeof(double), typeof(ScaleToAnimation), 1.0);

    public double Scale
    {
        get => (double)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public override Task Animate(VisualElement view, CancellationToken token = default)
    {
        if (Math.Abs(view.Scale - Scale) < 0.001)
        {
            return Task.CompletedTask;
        }

        return view.ScaleToAsync(Scale, Length, Easing);
    }
}
