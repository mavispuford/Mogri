using Mogri.ViewModels;
using SkiaSharp;

namespace Mogri.Helpers;

/// <summary>
/// Provides hit-testing helpers for editable canvas text elements.
/// </summary>
public static class CanvasTextHitTester
{
    public static TextElementViewModel? GetHitTextElement(
        IEnumerable<TextElementViewModel>? textElements,
        SKPoint imageLocation,
        float textSelectionPadding,
        float minTextScale)
    {
        if (textElements == null)
        {
            return null;
        }

        foreach (var textElement in textElements.OrderByDescending(textElement => textElement.Order))
        {
            if (string.IsNullOrWhiteSpace(textElement.Text))
            {
                continue;
            }

            var bounds = TextElementLayoutHelper.GetTextBoundsWithFallback(textElement.Text, textElement.BaseFontSize);
            if (bounds.IsEmpty)
            {
                continue;
            }

            var localPoint = CanvasTextGeometryHelper.GetLocalTextPoint(
                imageLocation,
                textElement.X,
                textElement.Y,
                textElement.Rotation,
                textElement.Scale,
                minTextScale,
                bounds);
            var hitBounds = CanvasTextGeometryHelper.GetInflatedBounds(bounds, textSelectionPadding);

            if (hitBounds.Contains(localPoint))
            {
                return textElement;
            }
        }

        return null;
    }
}