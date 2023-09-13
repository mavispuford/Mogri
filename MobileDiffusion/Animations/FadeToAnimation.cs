using CommunityToolkit.Maui.Animations;

namespace MobileDiffusion.Animations;

class FadeToAnimation : BaseAnimation
{
    public double Opacity { get; set; }

    public override Task Animate(VisualElement view) => view.FadeTo(Opacity, Length, Easing);
}
