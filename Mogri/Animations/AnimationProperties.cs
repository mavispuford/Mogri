using CommunityToolkit.Maui.Animations;

namespace Mogri.Animations;

/// <summary>
///     Attached property which sets animations on a <see cref="VisualElement"/>. To be used in a <see cref="VisualStateGroup"/>.
/// </summary>
/// <remarks>
/// There are two ways of using this. You can set a single animation, or an array of animations.
/// Single:
/// <code>
///     &lt;AnimationProperties.Animations&gt;
///             &lt;ScaleToAnimation Scale = "1" Easing="{x:Static Easing.CubicInOut}" Length="200" /&gt;
///     &lt;/AnimationProperties.Animations&gt;
/// </code>
/// 
/// Array:
/// <code>
///     &lt;x:Array Type="{x:Type toolkit:BaseAnimation}"&gt;
///         &lt;ScaleToAnimation Scale = "1" Easing="{x:Static Easing.CubicInOut}" Length="200" /&gt;
///         &lt;ColorToAnimation FromColor = "Black" ToColor="Red" BindableProperty="{x:Static VisualElement.BackgroundColorProperty}" /&gt;
///     &lt;/x:Array&gt;
/// </code>
/// </remarks>
public static class AnimationProperties
{
    public static readonly BindableProperty AnimationsProperty =
        BindableProperty.CreateAttached("Animations", typeof(object), typeof(VisualElement), null, propertyChanged: (bindable, oldValue, newValue) =>
        {
            if (bindable is VisualElement element)
            {
                element.BindingContextChanged -= OnBindingContextChanged;
                element.BindingContextChanged += OnBindingContextChanged;

                ApplyAnimations(element, newValue);
            }
        });

    private static void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (sender is not VisualElement element)
        {
            return;
        }

        var animations = GetAnimations(element);

        if (animations is BaseAnimation[] animationArray)
        {
            foreach (var animation in animationArray)
            {
                animation.BindingContext = element.BindingContext;
            }
        }
        else if (animations is BaseAnimation animation)
        {
            animation.BindingContext = element.BindingContext;
        }
    }

    private static void ApplyAnimations(VisualElement element, object newValue)
    {
        if (newValue is BaseAnimation[] animations)
        {
            foreach (var animation in animations)
            {
                animation.BindingContext = element.BindingContext;
                animation.PropertyChanged -= (s, e) => OnAnimationPropertyChanged(s, e, element);
                animation.PropertyChanged += (s, e) => OnAnimationPropertyChanged(s, e, element);
                animation.Animate(element);
            }
        }
        else if (newValue is BaseAnimation animation)
        {
            animation.BindingContext = element.BindingContext;
            animation.PropertyChanged -= (s, e) => OnAnimationPropertyChanged(s, e, element);
            animation.PropertyChanged += (s, e) => OnAnimationPropertyChanged(s, e, element);
            animation.Animate(element);
        }
    }

    private static void OnAnimationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e, VisualElement element)
    {
        if (e.PropertyName != nameof(BaseAnimation.BindingContext))
        {
            if (sender is BaseAnimation animation)
            {
                animation.Animate(element);
            }
        }
    }

    public static object GetAnimations(BindableObject view)
    {
        return view.GetValue(AnimationsProperty);
    }

    public static void SetAnimations(BindableObject view, object value)
    {
        view.SetValue(AnimationsProperty, value);
    }
}
