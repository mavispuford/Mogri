using Mogri.ViewModels;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using SkiaSharp.Views.Maui;
using System.Globalization;
using System.Text;

namespace Mogri.Helpers;

/// <summary>
/// Provides canvas text rendering helpers for fallback fonts, noise overlays, and selection chrome.
/// </summary>
public static class CanvasTextRenderer
{
    public static void DrawSelectionOutline(
        SKCanvas canvas,
        TextElementViewModel textElement,
        float canvasScale,
        float textSelectionPadding,
        float textSelectionCornerRadius,
        float textSelectionShadowStroke,
        float textSelectionStroke)
    {
        var bounds = TextElementLayoutHelper.GetTextBoundsWithFallback(textElement.Text, textElement.BaseFontSize);
        if (bounds.IsEmpty)
        {
            return;
        }

        var selectionBounds = CanvasTextGeometryHelper.GetInflatedBounds(bounds, textSelectionPadding);

        canvas.Save();

        if (canvasScale != 1f)
        {
            canvas.Scale(canvasScale);
        }

        canvas.Translate(textElement.X, textElement.Y);
        canvas.RotateDegrees(textElement.Rotation);
        canvas.Scale(textElement.Scale);
        canvas.Translate(-bounds.MidX, -bounds.MidY);

        using var shadowPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(96),
            IsAntialias = true,
            StrokeWidth = textSelectionShadowStroke,
            Style = SKPaintStyle.Stroke
        };
        using var outlinePaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(235),
            IsAntialias = true,
            StrokeWidth = textSelectionStroke,
            Style = SKPaintStyle.Stroke
        };

        canvas.DrawRoundRect(selectionBounds, textSelectionCornerRadius, textSelectionCornerRadius, shadowPaint);
        canvas.DrawRoundRect(selectionBounds, textSelectionCornerRadius, textSelectionCornerRadius, outlinePaint);

        canvas.Restore();
    }

    public static void DrawTextElement(SKCanvas canvas, TextElementViewModel textElement, float canvasScale = 1f)
    {
        if (string.IsNullOrEmpty(textElement.Text))
        {
            return;
        }

        var bounds = TextElementLayoutHelper.GetTextBoundsWithFallback(textElement.Text, textElement.BaseFontSize);

        canvas.Save();

        if (canvasScale != 1f)
        {
            canvas.Scale(canvasScale);
        }

        canvas.Translate(textElement.X, textElement.Y);
        canvas.RotateDegrees(textElement.Rotation);
        canvas.Scale(textElement.Scale);
        canvas.Translate(-bounds.MidX, -bounds.MidY);
        drawTextWithFallback(canvas, textElement.Text, textElement.Color, textElement.Alpha, textElement.Noise, textElement.BaseFontSize);

        canvas.Restore();
    }

    public static SKBitmap PrepareSourceBitmapWithText(SKBitmap sourceBitmap, IEnumerable<TextElementViewModel> textElements)
    {
        var info = new SKImageInfo(sourceBitmap.Width, sourceBitmap.Height, sourceBitmap.ColorType, sourceBitmap.AlphaType, sourceBitmap.ColorSpace);
        var preparedBitmap = new SKBitmap(info);

        using var canvas = new SKCanvas(preparedBitmap);
        canvas.DrawBitmap(sourceBitmap, 0, 0);

        foreach (var textElement in textElements.OrderBy(textElement => textElement.Order))
        {
            DrawTextElement(canvas, textElement);
        }

        return preparedBitmap;
    }

    private static void drawTextWithFallback(SKCanvas canvas, string text, Color color, float alpha, double noise, float baseFontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var skColor = color.ToSKColor().WithAlpha((byte)Math.Clamp((int)Math.Round(alpha * 255f), 0, 255));

        if (noise <= 0d)
        {
            processTextRunsWithFallback(text, baseFontSize, skColor, (textRun, font, paint, shaper, _, originX) =>
            {
                canvas.DrawShapedText(shaper, textRun.Text, originX, 0f, font, paint);
            });

            return;
        }

        processTextRunsWithFallback(text, baseFontSize, skColor, (textRun, font, paint, shaper, runBounds, originX) =>
        {
            if (textRun.PreserveIntrinsicGlyphDetails)
            {
                drawIntrinsicGlyphNoiseOverlay(canvas, textRun, font, paint, shaper, runBounds, originX, noise);
                return;
            }

            var layerBounds = getTextRunLayerBounds(runBounds);
            if (layerBounds.Width <= 0f || layerBounds.Height <= 0f)
            {
                return;
            }

            paint.Color = SKColors.White;
            paint.Shader = null;

            using var fillPaint = new SKPaint();
            using var fillShader = configureTextFillPaint(fillPaint, skColor, noise);

            // Fill through a text alpha mask so standard text keeps the current replacement-fill behavior.
            fillPaint.BlendMode = SKBlendMode.SrcIn;
            canvas.SaveLayer(layerBounds, null);
            canvas.DrawShapedText(shaper, textRun.Text, originX, 0f, font, paint);
            canvas.DrawPaint(fillPaint);
            canvas.Restore();
        });
    }

    private static void drawIntrinsicGlyphNoiseOverlay(
        SKCanvas canvas,
        TextRun textRun,
        SKFont font,
        SKPaint paint,
        SKShaper shaper,
        SKRect runBounds,
        float originX,
        double noise)
    {
        canvas.DrawShapedText(shaper, textRun.Text, originX, 0f, font, paint);

        var layerBounds = getTextRunLayerBounds(runBounds);
        if (layerBounds.Width <= 0f || layerBounds.Height <= 0f)
        {
            return;
        }

        using var overlayShader = NoiseShaderHelper.CreateNeutralNoiseOverlayShader(noise);
        if (overlayShader == null)
        {
            return;
        }

        using var compositePaint = new SKPaint
        {
            BlendMode = SKBlendMode.Overlay,
            Color = SKColors.White.WithAlpha((byte)Math.Clamp((int)Math.Round(getIntrinsicGlyphNoiseOverlayOpacity(noise) * 255f), 0, 255))
        };
        using var overlayPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.SrcIn,
            Shader = overlayShader
        };

        canvas.SaveLayer(layerBounds, compositePaint);
        canvas.DrawShapedText(shaper, textRun.Text, originX, 0f, font, paint);
        canvas.DrawPaint(overlayPaint);
        canvas.Restore();
    }

    private static double getIntrinsicGlyphNoiseOverlayOpacity(double noise)
    {
        return Math.Clamp((noise * 0.45d) + (noise * noise * 0.45d), 0d, 0.9d);
    }

    private static SKShader? configureTextFillPaint(SKPaint paint, SKColor color, double noise)
    {
        paint.IsAntialias = true;
        paint.Style = SKPaintStyle.Fill;

        if (noise > 0d)
        {
            var shader = NoiseShaderHelper.CreateNoiseShader(color, noise);
            if (shader != null)
            {
                paint.Shader = shader;
                paint.Color = SKColors.White.WithAlpha(color.Alpha);
                return shader;
            }
        }

        paint.Shader = null;
        paint.Color = color;
        return null;
    }

    private static SKRect getTextRunLayerBounds(SKRect runBounds)
    {
        const float textRunLayerPadding = 2f;

        return new SKRect(
            runBounds.Left - textRunLayerPadding,
            runBounds.Top - textRunLayerPadding,
            runBounds.Right + textRunLayerPadding,
            runBounds.Bottom + textRunLayerPadding);
    }

    private static void processTextRunsWithFallback(
        string text,
        float baseFontSize,
        SKColor color,
        Action<TextRun, SKFont, SKPaint, SKShaper, SKRect, float> processRun)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var textRuns = buildTextRunsWithFallback(text);

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

                var runBounds = getRunBounds(font, shapedText.Points, glyphBounds, glyphWidths, currentX);
                processRun(textRun, font, paint, shaper, runBounds, currentX);

                currentX += getRunAdvance(shapedText.Points, glyphWidths, currentX);
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

    private static List<TextRun> buildTextRunsWithFallback(string text)
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
        var currentPreserveIntrinsicGlyphDetails = false;

        var textElementEnumerator = StringInfo.GetTextElementEnumerator(text);
        while (textElementEnumerator.MoveNext())
        {
            var textElement = textElementEnumerator.GetTextElement();
            var (typeface, ownsTypeface) = getTypefaceForTextElement(primaryTypeface, textElement);
            var preserveIntrinsicGlyphDetails = shouldPreserveIntrinsicGlyphDetails(textElement);

            if (currentTypeface == null)
            {
                currentText.Append(textElement);
                currentTypeface = typeface;
                currentOwnsTypeface = ownsTypeface;
                currentPreserveIntrinsicGlyphDetails = preserveIntrinsicGlyphDetails;
                continue;
            }

            if (currentPreserveIntrinsicGlyphDetails == preserveIntrinsicGlyphDetails
                && areEquivalentTypefaces(currentTypeface, typeface))
            {
                currentText.Append(textElement);

                if (ownsTypeface && !ReferenceEquals(typeface, currentTypeface))
                {
                    typeface.Dispose();
                }

                continue;
            }

            textRuns.Add(new TextRun(currentText.ToString(), currentTypeface, currentOwnsTypeface, currentPreserveIntrinsicGlyphDetails));
            currentText.Clear();
            currentText.Append(textElement);
            currentTypeface = typeface;
            currentOwnsTypeface = ownsTypeface;
            currentPreserveIntrinsicGlyphDetails = preserveIntrinsicGlyphDetails;
        }

        if (currentTypeface != null && currentText.Length > 0)
        {
            textRuns.Add(new TextRun(currentText.ToString(), currentTypeface, currentOwnsTypeface, currentPreserveIntrinsicGlyphDetails));
        }

        return textRuns;
    }

    private static bool shouldPreserveIntrinsicGlyphDetails(string textElement)
    {
        if (string.IsNullOrEmpty(textElement))
        {
            return false;
        }

        foreach (var rune in textElement.EnumerateRunes())
        {
            var value = rune.Value;

            if (value is 0x200D or 0x20E3 or 0xFE0F)
            {
                return true;
            }

            if ((value >= 0x1F1E6 && value <= 0x1FAFF)
                || (value >= 0x1F3FB && value <= 0x1F3FF))
            {
                return true;
            }
        }

        return false;
    }

    private static (SKTypeface Typeface, bool OwnsTypeface) getTypefaceForTextElement(SKTypeface primaryTypeface, string textElement)
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

    private static bool areEquivalentTypefaces(SKTypeface left, SKTypeface right)
    {
        return left.FamilyName == right.FamilyName && left.FontStyle == right.FontStyle;
    }

    private static SKRect getRunBounds(SKFont font, SKPoint[] glyphPoints, SKRect[] glyphBounds, float[] glyphWidths, float originX)
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

            runBounds = unionRects(runBounds, positionedBounds);
        }

        if (hasBounds)
        {
            return runBounds;
        }

        var advance = getRunAdvance(glyphPoints, glyphWidths, originX);
        var metrics = font.Metrics;
        return new SKRect(originX, metrics.Ascent, originX + advance, metrics.Descent);
    }

    private static float getRunAdvance(SKPoint[] glyphPoints, float[] glyphWidths, float originX)
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

    private static SKRect unionRects(SKRect left, SKRect right)
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

        public TextRun(string text, SKTypeface typeface, bool ownsTypeface, bool preserveIntrinsicGlyphDetails)
        {
            Text = text;
            Typeface = typeface;
            _ownsTypeface = ownsTypeface;
            PreserveIntrinsicGlyphDetails = preserveIntrinsicGlyphDetails;
        }

        public string Text { get; }

        public SKTypeface Typeface { get; }

        public bool PreserveIntrinsicGlyphDetails { get; }

        public void Dispose()
        {
            if (_ownsTypeface)
            {
                Typeface.Dispose();
            }
        }
    }
}