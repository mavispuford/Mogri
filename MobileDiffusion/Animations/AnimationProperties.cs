using CommunityToolkit.Maui.Animations;

namespace MobileDiffusion.Animations;

/// <summary>
///     Attached property which sets animations on a <see cref="VisualElement"/>. To be used in a <see cref="VisualStateGroup"/>.
/// </summary>
/// <remarks>
/// There are two ways of using this. You can set a single animation, or an array of animations.
/// Single:
/// <code>
///     <animations:AnimationProperties.Animations>
///             <animations:ScaleToAnimation Scale = "1" Easing="{x:Static Easing.CubicInOut}" Length="200" />
///     </animations:AnimationProperties.Animations>
/// </code>
/// 
/// Array:
/// <code>
///     <x:Array Type="{x:Type toolkit:BaseAnimation}">
///         <animations:ScaleToAnimation Scale = "1" Easing="{x:Static Easing.CubicInOut}" Length="200" />
///         <animations:ColorToAnimation FromColor = "Black" ToColor="Red" BindableProperty="{x:Static VisualElement.BackgroundColorProperty}" />
///     </x:Array>
/// </code>
/// </remarks>
public static class AnimationProperties
{
    public static readonly BindableProperty AnimationsProperty =
        BindableProperty.CreateAttached("Animations", typeof(object), typeof(VisualElement), null, propertyChanged: (bindable, oldValue, newValue) =>
        {
            if (bindable is VisualElement element)
            {
                if (newValue is BaseAnimation[] animations)
                {
                    foreach (var animation in animations)
                    {
                        animation.Animate(element);
                    }
                }
                else if (newValue is BaseAnimation animation)
                {
                    animation.Animate(element);
                }
            }
            
        });

    public static object GetAnimations(BindableObject view)
    {
        return view.GetValue(AnimationsProperty);
    }

    public static void SetAnimations(BindableObject view, object value)
    {
        view.SetValue(AnimationsProperty, value);
    }
}
