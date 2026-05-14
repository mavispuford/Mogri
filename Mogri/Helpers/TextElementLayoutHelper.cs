using Mogri.ViewModels;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using System.Globalization;
using System.Text;

namespace Mogri.Helpers;

/// <summary>
/// Provides shared text layout and bounds calculations for editable text elements.
/// </summary>
public static class TextElementLayoutHelper
{
    public static SKRect GetTextBoundsWithFallback(string text, float baseFontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return SKRect.Empty;
        }

        var combinedBounds = SKRect.Empty;
        var hasBounds = false;

        ProcessTextRunsWithFallback(text, baseFontSize, SKColors.White, (_, _, _, _, runBounds, _) =>
        {
            if (!hasBounds)
            {
                combinedBounds = runBounds;
                hasBounds = true;
                return;
            }

            combinedBounds = UnionRects(combinedBounds, runBounds);
        });

        return combinedBounds;
    }

    public static SKRect GetAxisAlignedBounds(TextElementViewModel textElement)
    {
        if (string.IsNullOrWhiteSpace(textElement.Text))
        {
            return SKRect.Empty;
        }

        var bounds = GetTextBoundsWithFallback(textElement.Text, textElement.BaseFontSize);
        if (bounds.IsEmpty)
        {
            return SKRect.Empty;
        }

        var center = new SKPoint(bounds.MidX, bounds.MidY);
        var radians = textElement.Rotation * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);

        var transformedCorners = new[]
        {
            TransformCorner(bounds.Left, bounds.Top, center, textElement, cos, sin),
            TransformCorner(bounds.Right, bounds.Top, center, textElement, cos, sin),
            TransformCorner(bounds.Right, bounds.Bottom, center, textElement, cos, sin),
            TransformCorner(bounds.Left, bounds.Bottom, center, textElement, cos, sin)
        };

        var left = transformedCorners.Min(point => point.X);
        var top = transformedCorners.Min(point => point.Y);
        var right = transformedCorners.Max(point => point.X);
        var bottom = transformedCorners.Max(point => point.Y);

        return new SKRect(left, top, right, bottom);
    }

    private static SKPoint TransformCorner(
        float localX,
        float localY,
        SKPoint center,
        TextElementViewModel textElement,
        float cos,
        float sin)
    {
        var offsetX = (localX - center.X) * textElement.Scale;
        var offsetY = (localY - center.Y) * textElement.Scale;

        var rotatedX = offsetX * cos - offsetY * sin;
        var rotatedY = offsetX * sin + offsetY * cos;

        return new SKPoint(textElement.X + rotatedX, textElement.Y + rotatedY);
    }

    private static void ProcessTextRunsWithFallback(
        string text,
        float baseFontSize,
        SKColor color,
        Action<string, SKFont, SKPaint, SKShaper, SKRect, float> processRun)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var textRuns = BuildTextRunsWithFallback(text);

        try
        {
            var currentX = 0f;

            foreach (var textRun in textRuns)
            {
                using var font = new SKFont(textRun.Typeface, baseFontSize)
                {
                    Edging = SKFontEdging.Antialias,
                    LinearMetrics = true,
                    Subpixel = true
                };
                using var paint = new SKPaint
                {
                    Color = color,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                using var shaper = new SKShaper(textRun.Typeface);

                var shapedText = shaper.Shape(textRun.Text, currentX, 0f, font);
                var glyphs = shapedText.Codepoints.Select(static codepoint => (ushort)codepoint).ToArray();
                var glyphWidths = new float[glyphs.Length];
                var glyphBounds = new SKRect[glyphs.Length];

                if (glyphs.Length > 0)
                {
                    font.GetGlyphWidths(glyphs, glyphWidths, glyphBounds, paint);
                }

                var runBounds = GetRunBounds(font, shapedText.Points, glyphBounds, glyphWidths, currentX);
                processRun(textRun.Text, font, paint, shaper, runBounds, currentX);

                currentX += GetRunAdvance(shapedText.Points, glyphWidths, currentX);
            }
        }
        finally
        {
            foreach (var textRun in textRuns)
            {
                textRun.Dispose();
            }
        }
    }

    private static List<TextRun> BuildTextRunsWithFallback(string text)
    {
        var textRuns = new List<TextRun>();
        if (string.IsNullOrEmpty(text))
        {
            return textRuns;
        }

        var primaryTypeface = SKTypeface.Default;
        var currentText = new StringBuilder();
        SKTypeface? currentTypeface = null;
        var currentOwnsTypeface = false;

        var textElementEnumerator = StringInfo.GetTextElementEnumerator(text);
        while (textElementEnumerator.MoveNext())
        {
            var textElement = textElementEnumerator.GetTextElement();
            var (typeface, ownsTypeface) = GetTypefaceForTextElement(primaryTypeface, textElement);

            if (currentTypeface == null)
            {
                currentText.Append(textElement);
                currentTypeface = typeface;
                currentOwnsTypeface = ownsTypeface;
                continue;
            }

            if (AreEquivalentTypefaces(currentTypeface, typeface))
            {
                currentText.Append(textElement);

                if (ownsTypeface && !ReferenceEquals(typeface, currentTypeface))
                {
                    typeface.Dispose();
                }

                continue;
            }

            textRuns.Add(new TextRun(currentText.ToString(), currentTypeface, currentOwnsTypeface));
            currentText.Clear();
            currentText.Append(textElement);
            currentTypeface = typeface;
            currentOwnsTypeface = ownsTypeface;
        }

        if (currentTypeface != null && currentText.Length > 0)
        {
            textRuns.Add(new TextRun(currentText.ToString(), currentTypeface, currentOwnsTypeface));
        }

        return textRuns;
    }

    private static (SKTypeface Typeface, bool OwnsTypeface) GetTypefaceForTextElement(SKTypeface primaryTypeface, string textElement)
    {
        using var primaryFont = new SKFont(primaryTypeface, 12f);
        if (primaryFont.ContainsGlyphs(textElement))
        {
            return (primaryTypeface, false);
        }

        var firstCodePoint = Rune.GetRuneAt(textElement, 0).Value;
        var fallbackTypeface = SKFontManager.Default.MatchCharacter(primaryTypeface.FamilyName, primaryTypeface.FontStyle, Array.Empty<string>(), firstCodePoint);

        if (fallbackTypeface == null)
        {
            return (primaryTypeface, false);
        }

        return (fallbackTypeface, !ReferenceEquals(fallbackTypeface, primaryTypeface));
    }

    private static bool AreEquivalentTypefaces(SKTypeface left, SKTypeface right)
    {
        return left.FamilyName == right.FamilyName && left.FontStyle == right.FontStyle;
    }

    private static SKRect GetRunBounds(SKFont font, SKPoint[] glyphPoints, SKRect[] glyphBounds, float[] glyphWidths, float originX)
    {
        var hasBounds = false;
        var runBounds = SKRect.Empty;

        for (var i = 0; i < glyphBounds.Length; i++)
        {
            var positionedBounds = glyphBounds[i];
            if (i < glyphPoints.Length)
            {
                positionedBounds.Offset(glyphPoints[i]);
            }
            else
            {
                positionedBounds.Offset(originX, 0f);
            }

            if (!hasBounds)
            {
                runBounds = positionedBounds;
                hasBounds = true;
                continue;
            }

            runBounds = UnionRects(runBounds, positionedBounds);
        }

        if (hasBounds)
        {
            return runBounds;
        }

        var advance = GetRunAdvance(glyphPoints, glyphWidths, originX);
        var metrics = font.Metrics;
        return new SKRect(originX, metrics.Ascent, originX + advance, metrics.Descent);
    }

    private static float GetRunAdvance(SKPoint[] glyphPoints, float[] glyphWidths, float originX)
    {
        var rightMost = originX;

        for (var i = 0; i < glyphWidths.Length; i++)
        {
            var pointX = i < glyphPoints.Length ? glyphPoints[i].X : originX;
            var candidateRight = pointX + glyphWidths[i];
            if (candidateRight > rightMost)
            {
                rightMost = candidateRight;
            }
        }

        return Math.Max(0f, rightMost - originX);
    }

    private static SKRect UnionRects(SKRect left, SKRect right)
    {
        return new SKRect(
            Math.Min(left.Left, right.Left),
            Math.Min(left.Top, right.Top),
            Math.Max(left.Right, right.Right),
            Math.Max(left.Bottom, right.Bottom));
    }

    private sealed class TextRun : IDisposable
    {
        private readonly bool _ownsTypeface;

        public TextRun(string text, SKTypeface typeface, bool ownsTypeface)
        {
            Text = text;
            Typeface = typeface;
            _ownsTypeface = ownsTypeface;
        }

        public string Text { get; }

        public SKTypeface Typeface { get; }

        public void Dispose()
        {
            if (_ownsTypeface)
            {
                Typeface.Dispose();
            }
        }
    }
}