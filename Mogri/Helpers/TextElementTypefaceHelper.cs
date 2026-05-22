using SkiaSharp;

namespace Mogri.Helpers;

/// <summary>
/// Selects the most appropriate typeface for a text element, including emoji-aware fallback behavior.
/// </summary>
public static class TextElementTypefaceHelper
{
    private static readonly string[] EmojiLanguageTags = ["und-Zsye"];

    public static (SKTypeface Typeface, bool OwnsTypeface) GetTypefaceForTextElement(SKTypeface primaryTypeface, string textElement)
    {
        using var primaryFont = new SKFont(primaryTypeface, 12f);
        var primaryContainsGlyphs = primaryFont.ContainsGlyphs(textElement);

        if (EmojiPresentationHelper.TryGetPreferredEmojiTypefaceFallbackCodePoint(textElement, out var emojiFallbackCodePoint))
        {
            var emojiTypeface = tryGetExplicitEmojiTypeface(primaryTypeface, emojiFallbackCodePoint);
            if (emojiTypeface != null)
            {
                return (emojiTypeface, !ReferenceEquals(emojiTypeface, primaryTypeface));
            }
        }

        if (primaryContainsGlyphs)
        {
            return (primaryTypeface, false);
        }

        var fallbackCodePoint = EmojiPresentationHelper.GetPreferredTypefaceFallbackCodePoint(textElement);
        var fallbackTypeface = fallbackCodePoint == 0 ? null : SKFontManager.Default.MatchCharacter(fallbackCodePoint);

        if (fallbackTypeface == null)
        {
            return (primaryTypeface, false);
        }

        return (fallbackTypeface, !ReferenceEquals(fallbackTypeface, primaryTypeface));
    }

    private static SKTypeface? tryGetExplicitEmojiTypeface(SKTypeface primaryTypeface, int emojiFallbackCodePoint)
    {
        foreach (var familyName in getPreferredEmojiFamilies())
        {
            var familyTypeface = SKTypeface.FromFamilyName(familyName);
            if (familyTypeface == null)
            {
                continue;
            }

            var anchorCoverage = hasGlyphCoverage(familyTypeface, emojiFallbackCodePoint);
            var preferredEmojiFamily = isPreferredEmojiFamilyTypeface(familyTypeface, familyName);

            if (preferredEmojiFamily && anchorCoverage)
            {
                return familyTypeface;
            }

            if (!ReferenceEquals(familyTypeface, primaryTypeface))
            {
                familyTypeface.Dispose();
            }
        }

        var emojiTypeface = SKFontManager.Default.MatchCharacter(string.Empty, primaryTypeface.FontStyle, EmojiLanguageTags, emojiFallbackCodePoint);
        if (emojiTypeface != null)
        {
            var anchorCoverage = hasGlyphCoverage(emojiTypeface, emojiFallbackCodePoint);

            if (anchorCoverage)
            {
                return emojiTypeface;
            }

            if (!ReferenceEquals(emojiTypeface, primaryTypeface))
            {
                emojiTypeface.Dispose();
            }
        }

        emojiTypeface = SKFontManager.Default.MatchCharacter(emojiFallbackCodePoint);
        if (emojiTypeface != null)
        {
            var anchorCoverage = hasGlyphCoverage(emojiTypeface, emojiFallbackCodePoint);
            var emojiTypefaceMatch = isEmojiTypeface(emojiTypeface);

            if (emojiTypefaceMatch && anchorCoverage)
            {
                return emojiTypeface;
            }
        }

        if (emojiTypeface != null && !ReferenceEquals(emojiTypeface, primaryTypeface))
        {
            emojiTypeface.Dispose();
        }

        return null;
    }

    private static bool hasGlyphCoverage(SKTypeface typeface, int codePoint)
    {
        using var font = new SKFont(typeface, 12f);
        return font.ContainsGlyph(codePoint);
    }

    private static bool isEmojiTypeface(SKTypeface typeface)
    {
        return typeface.FamilyName.Contains("Emoji", StringComparison.OrdinalIgnoreCase);
    }

    private static bool isPreferredEmojiFamilyTypeface(SKTypeface typeface, string requestedFamilyName)
    {
        return typeface.FamilyName.Equals(requestedFamilyName, StringComparison.OrdinalIgnoreCase)
            || isEmojiTypeface(typeface);
    }

    private static IReadOnlyList<string> getPreferredEmojiFamilies()
    {
        if (OperatingSystem.IsAndroid())
        {
            return ["Noto Color Emoji", "Noto Color Emoji Compat"];
        }

        if (OperatingSystem.IsIOS())
        {
            return ["Apple Color Emoji"];
        }

        return [];
    }
}