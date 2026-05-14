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
        var unscaledPoint = new SKPoint(rotatedPoint.X / safeScale, rotatedPoint.Y / safeScale);

        return new SKPoint(unscaledPoint.X + bounds.MidX, unscaledPoint.Y + bounds.MidY);
    }
}