namespace MobileDiffusion.Helpers;

public static class MathHelper
{
    /// <summary>
    ///     Clamps an integer value between the min and max.
    /// </summary>
    /// <param name="value">The input value.</param>
    /// <param name="min">The minimum.</param>
    /// <param name="max">The maximum.</param>
    /// <returns>The clamped integer value.</returns>
    public static int Clamp(int value, int min, int max)
    {
        return Math.Max(Math.Min(value, max), min);
    }
}
