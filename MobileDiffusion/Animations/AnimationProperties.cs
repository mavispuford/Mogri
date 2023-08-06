using CommunityToolkit.Maui.Animations;

namespace MobileDiffusion.Animations;

public static class AnimationProperties
{
    public static readonly BindableProperty AnimationProperty =
        BindableProperty.CreateAttached("Animation", typeof(BaseAnimation), typeof(VisualElement), null, propertyChanged: (bindable, oldValue, newValue) =>
        {
            if (newValue is BaseAnimation animation &&
                bindable is VisualElement element)
            {
                animation.Animate(element);
            }
        });

    public static BaseAnimation GetAnimation(BindableObject view)
    {
        return (BaseAnimation)view.GetValue(AnimationProperty);
    }

    public static void SetAnimation(BindableObject view, BaseAnimation value)
    {
        view.SetValue(AnimationProperty, value);
    }
}
