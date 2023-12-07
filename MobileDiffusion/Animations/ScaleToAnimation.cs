using CommunityToolkit.Maui.Animations;

namespace MobileDiffusion.Animations;

class ScaleToAnimation : BaseAnimation
{
    public double Scale { get; set; }

    public override Task Animate(VisualElement view, CancellationToken token = default) => view.ScaleTo(Scale, Length, Easing);
}
