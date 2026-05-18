using SkiaSharp;

namespace Mogri.Helpers;

/// <summary>
/// Provides deterministic geometry helpers used by canvas text rendering and hit testing.
/// </summary>
public static class CanvasTextGeometryHelper
{
    public static SKRect GetInflatedBounds(SKRect bounds, float padding)
    {
        var inflatedBounds = bounds;
        inflatedBounds.Inflate(padding, padding);
        return inflatedBounds;
    }

    public static SKPoint GetLocalTextPoint(
        SKPoint imageLocation,
        float textX,
        float textY,
        float rotationDegrees,
        float scale,
        float scaleXMultiplier,
        float scaleYMultiplier,
        float minScale,
        SKRect bounds)
    {
        var translatedPoint = new SKPoint(imageLocation.X - textX, imageLocation.Y - textY);
        var rotationRadians = -rotationDegrees * (MathF.PI / 180f);
        var cos = MathF.Cos(rotationRadians);
        var sin = MathF.Sin(rotationRadians);
        var rotatedPoint = new SKPoint(
            translatedPoint.X * cos - translatedPoint.Y * sin,
            translatedPoint.X * sin + translatedPoint.Y * cos);
        var safeScale = Math.Max(scale, minScale);
        var safeScaleX = safeScale * getSafeAxisMultiplier(scaleXMultiplier);
        var safeScaleY = safeScale * getSafeAxisMultiplier(scaleYMultiplier);
        var unscaledPoint = new SKPoint(rotatedPoint.X / safeScaleX, rotatedPoint.Y / safeScaleY);

        return new SKPoint(unscaledPoint.X + bounds.MidX, unscaledPoint.Y + bounds.MidY);
    }

    private static float getSafeAxisMultiplier(float value)
    {
        return value == 0f ? 1f : value;
    }
}