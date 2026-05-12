using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Mogri.Interfaces.Services;

namespace Mogri.Services;

/// <summary>
/// Adapter around the MAUI page animation APIs used for generation progress.
/// </summary>
public class AnimationService : IAnimationService
{
    public void AnimateProgress(float start, float end, Action<float> onUpdate)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var page = getCurrentPage();

            page.AbortAnimation("ProgressAnimation");

            var animation = new Animation(value =>
            {
                onUpdate((float)value);
            }, start, end, Easing.SinOut);

            animation.Commit(page, "ProgressAnimation", length: 500);
        });
    }

    private static Page getCurrentPage()
    {
        return Shell.Current?.CurrentPage ?? throw new InvalidOperationException("Current page is not available.");
    }
}