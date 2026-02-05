using CommunityToolkit.Maui.Animations;

namespace MobileDiffusion.Animations;

public class TranslateToAnimation : BaseAnimation
{
    public static readonly BindableProperty TranslationXProperty = BindableProperty.Create(nameof(TranslationX),
        typeof(double), typeof(TranslateToAnimation), 0d);

    public double TranslationX
    {
        get => (double)GetValue(TranslationXProperty);
        set => SetValue(TranslationXProperty, value);
    }

    public static readonly BindableProperty TranslationYProperty = BindableProperty.Create(nameof(TranslationY),
        typeof(double), typeof(TranslateToAnimation), 0d);

    public double TranslationY
    {
        get => (double)GetValue(TranslationYProperty);
        set => SetValue(TranslationYProperty, value);
    }

    public override Task Animate(VisualElement view, CancellationToken token = default) => view.TranslateToAsync(TranslationX, TranslationY, Length, Easing);
}
