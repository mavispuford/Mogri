#nullable enable

using CommunityToolkit.Maui.Animations;
using CommunityToolkit.Maui.Behaviors;
using System.Diagnostics;
using System.Windows.Input;

namespace Mogri.Animations;

/// <summary>
///     Modifies the MAUI Community Toolkit <see cref="AnimationBehavior"/> source to not add a tap gesture recognizer.
/// </summary>
public class CustomAnimationBehavior : EventToCommandBehavior
{
    /// <summary>
	///     Backing BindableProperty for the <see cref="AnimationType"/> property.
	/// </summary>
	public static readonly BindableProperty AnimationTypeProperty =
        BindableProperty.Create(nameof(AnimationType), typeof(BaseAnimation), typeof(CustomAnimationBehavior));

    /// <summary>
    ///     Backing BindableProperty for the <see cref="AnimateCommand"/> property.
    /// </summary>
    public static readonly BindableProperty AnimateCommandProperty =
        BindableProperty.CreateReadOnly(nameof(AnimateCommand), typeof(ICommand), typeof(CustomAnimationBehavior), default, BindingMode.OneWayToSource, defaultValueCreator: CreateAnimateCommand).BindableProperty;

    /// <summary>
    ///     Gets the Command that allows the triggering of the animation.
    /// </summary>
    public ICommand AnimateCommand => (ICommand)GetValue(AnimateCommandProperty);

    /// <summary>
    ///     The type of animation to perform.
    /// </summary>
    public BaseAnimation? AnimationType
    {
        get => (BaseAnimation?)GetValue(AnimationTypeProperty);
        set => SetValue(AnimationTypeProperty, value);
    }

    protected override void OnAttachedTo(BindableObject bindable)
    {
        base.OnAttachedTo(bindable);

        // Binding context isn't currently inherited from the parent, so we are manually setting/tracking it here.
        BindingContext = bindable.BindingContext;

        bindable.BindingContextChanged += OnBindableBindingContextChanged;
    }

    protected override void OnDetachingFrom(BindableObject bindable)
    {
        base.OnDetachingFrom(bindable);

        bindable.BindingContextChanged -= OnBindableBindingContextChanged;

        BindingContext = null;
    }

    void OnBindableBindingContextChanged(object? sender, EventArgs e)
    {
        if (sender is BindableObject bindable)
        {
            BindingContext = bindable.BindingContext;
        }
    }

    /// <inheritdoc/>
    protected override async void OnTriggerHandled(object? sender = null, object? eventArgs = null)
    {
        await OnAnimate();

        base.OnTriggerHandled(sender, eventArgs);
    }

    static object CreateAnimateCommand(BindableObject bindable)
        => new Command(async () => await ((CustomAnimationBehavior)bindable).OnAnimate().ConfigureAwait(false));

    Task OnAnimate()
    {
        if (View is null || AnimationType is null)
        {
            return Task.CompletedTask;
        }

        View.CancelAnimations();

        try
        {
            return AnimationType.Animate(View);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return Task.CompletedTask;
        }
    }
}
