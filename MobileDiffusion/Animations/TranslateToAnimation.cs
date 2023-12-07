using CommunityToolkit.Maui.Animations;

namespace MobileDiffusion.Animations;

class TranslateToAnimation : BaseAnimation
{
    public static BindableProperty TranslationXProperty = BindableProperty.Create(nameof(TranslationX),
        typeof(double), typeof(TranslateToAnimation), 0d);

    public double TranslationX
    {
        get => (double)GetValue(TranslationXProperty);
        set => SetValue(TranslationXProperty, value);
    }

    public static BindableProperty TranslationYProperty = BindableProperty.Create(nameof(TranslationY),
        typeof(double), typeof(TranslateToAnimation), 0d);

    public double TranslationY
    {
        get => (double)GetValue(TranslationYProperty);
        set => SetValue(TranslationYProperty, value);
    }

    public override Task Animate(VisualElement view, CancellationToken token = default) => view.TranslateTo(TranslationX, TranslationY, Length, Easing);
}
