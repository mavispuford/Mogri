using CommunityToolkit.Maui.Animations;
using MobileDiffusion.Helpers;

namespace MobileDiffusion.Animations;

public class ColorToAnimation : BaseAnimation
{
    public Color FromColor { get; set; }

    public Color ToColor { get; set; }

    public static readonly BindableProperty FromColorProperty = BindableProperty.Create(nameof(FromColor), typeof(Color), typeof(ColorToAnimation), Colors.Black);

    public BindableProperty BindableProperty { get; set; }

    public override Task Animate(VisualElement view) => view.ColorTo(FromColor, ToColor, color => {
        view.SetValue(BindableProperty, color);
    }, Length, Easing);
}
