namespace Mogri.Helpers;

public static class ViewExtensions
{
    public static Task<bool> ColorTo(this VisualElement self, Color fromColor, Color toColor, Action<Color> callback, uint length = 250, Easing? easing = null, string? name = null)
    {
        Func<double, Color> transform = (t) =>
            Color.FromRgba(fromColor.Red + t * (toColor.Red - fromColor.Red),
                           fromColor.Green + t * (toColor.Green - fromColor.Green),
                           fromColor.Blue + t * (toColor.Blue - fromColor.Blue),
                           fromColor.Alpha + t * (toColor.Alpha - fromColor.Alpha));

        return ColorAnimation(self, name ?? nameof(ColorTo), transform, callback, length, easing);
    }

    public static void CancelAnimations(this VisualElement self)
    {
        self.AbortAnimation(nameof(ColorTo));
    }

    static Task<bool> ColorAnimation(VisualElement element, string name, Func<double, Color> transform, Action<Color> callback, uint length, Easing? easing)
    {
        easing ??= Easing.Linear;

        var taskCompletionSource = new TaskCompletionSource<bool>();

        element.Animate(name, transform, callback, 16, length, easing, (v, c) => taskCompletionSource.SetResult(c));

        return taskCompletionSource.Task;
    }
}
