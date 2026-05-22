using System.Text;

namespace Mogri.Helpers;

/// <summary>
/// Identifies grapheme clusters that should keep their intrinsic glyph details when text noise is applied.
/// </summary>
public static class EmojiPresentationHelper
{
    public static bool ShouldPreserveIntrinsicGlyphDetails(string textElement)
    {
        if (string.IsNullOrEmpty(textElement))
        {
            return false;
        }

        var hasEmojiSignal = false;

        foreach (var rune in textElement.EnumerateRunes())
        {
            var value = rune.Value;

            if (value == 0xFE0E)
            {
                return false;
            }

            if (value is 0x200D or 0x20E3 or 0xFE0F)
            {
                hasEmojiSignal = true;
                continue;
            }

            if (isSupplementalEmojiCodePoint(value) || isLegacyEmojiSymbolCodePoint(value))
            {
                hasEmojiSignal = true;
            }
        }

        return hasEmojiSignal;
    }

    public static int GetPreferredTypefaceFallbackCodePoint(string textElement)
    {
        if (string.IsNullOrEmpty(textElement))
        {
            return 0;
        }

        int? keycapCodePoint = null;
        int? emojiCapableCodePoint = null;
        int? fallbackCodePoint = null;

        foreach (var rune in textElement.EnumerateRunes())
        {
            var value = rune.Value;

            if (fallbackCodePoint == null && !isTypefaceFallbackControlCodePoint(value))
            {
                fallbackCodePoint = value;
            }

            if (value == 0x20E3)
            {
                keycapCodePoint = value;
                continue;
            }

            if (isSupplementalEmojiCodePoint(value) || isLegacyEmojiSymbolCodePoint(value))
            {
                emojiCapableCodePoint ??= value;
            }
        }

        return keycapCodePoint
            ?? emojiCapableCodePoint
            ?? fallbackCodePoint
            ?? Rune.GetRuneAt(textElement, 0).Value;
    }

    public static bool TryGetPreferredEmojiTypefaceFallbackCodePoint(string textElement, out int codePoint)
    {
        codePoint = 0;

        if (string.IsNullOrEmpty(textElement))
        {
            return false;
        }

        var hasEmojiTypefaceSignal = false;

        foreach (var rune in textElement.EnumerateRunes())
        {
            var value = rune.Value;

            if (value == 0xFE0E)
            {
                codePoint = 0;
                return false;
            }

            if (value is 0x200D or 0x20E3 or 0xFE0F || isSupplementalEmojiCodePoint(value))
            {
                hasEmojiTypefaceSignal = true;
            }
        }

        if (!hasEmojiTypefaceSignal)
        {
            return false;
        }

        codePoint = GetPreferredTypefaceFallbackCodePoint(textElement);
        return codePoint != 0;
    }

    private static bool isSupplementalEmojiCodePoint(int value)
    {
        return (value >= 0x1F1E6 && value <= 0x1FAFF)
            || (value >= 0x1F3FB && value <= 0x1F3FF);
    }

    private static bool isLegacyEmojiSymbolCodePoint(int value)
    {
        return value is 0x00A9 or 0x00AE or 0x203C or 0x2049 or 0x2122 or 0x2139 or 0x2328 or 0x23CF or 0x24C2 or 0x3030 or 0x303D or 0x3297 or 0x3299
            || (value >= 0x2194 && value <= 0x2199)
            || (value >= 0x21A9 && value <= 0x21AA)
            || (value >= 0x231A && value <= 0x231B)
            || (value >= 0x23E9 && value <= 0x23FA)
            || (value >= 0x25AA && value <= 0x25AB)
            || value is 0x25B6 or 0x25C0
            || (value >= 0x25FB && value <= 0x25FE)
            || (value >= 0x2600 && value <= 0x27BF)
            || (value >= 0x2934 && value <= 0x2935)
            || (value >= 0x2B05 && value <= 0x2B07)
            || (value >= 0x2B1B && value <= 0x2B1C)
            || value is 0x2B50 or 0x2B55;
    }

    private static bool isTypefaceFallbackControlCodePoint(int value)
    {
        return value is 0x200D or 0xFE0E or 0xFE0F;
    }
}