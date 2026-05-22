using Mogri.Helpers;
using Xunit;

namespace Mogri.Tests.Helpers;

public class EmojiPresentationHelperTests
{
    [Theory]
    [InlineData("🤍")]
    [InlineData("🎅🏻")]
    [InlineData("👨🏻‍🍳")]
    [InlineData("🐻‍❄️")]
    [InlineData("🕊️")]
    [InlineData("⚠️")]
    [InlineData("3️⃣")]
    [InlineData("◽")]
    [InlineData("⚪")]
    [InlineData("⛄")]
    [InlineData("⛄️")]
    public void ShouldPreserveIntrinsicGlyphDetails_EmojiPresentationGlyph_ReturnsTrue(string textElement)
    {
        // Arrange

        // Act
        var result = EmojiPresentationHelper.ShouldPreserveIntrinsicGlyphDetails(textElement);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("7")]
    [InlineData(".")]
    [InlineData("⚠︎")]
    public void ShouldPreserveIntrinsicGlyphDetails_PlainTextGlyph_ReturnsFalse(string? textElement)
    {
        // Arrange

        // Act
        var result = EmojiPresentationHelper.ShouldPreserveIntrinsicGlyphDetails(textElement!);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("⚠️", 0x26A0)]
    [InlineData("3️⃣", 0x20E3)]
    [InlineData("👨🏻‍🍳", 0x1F468)]
    public void GetPreferredTypefaceFallbackCodePoint_EmojiSequence_ReturnsEmojiAnchor(string textElement, int expectedCodePoint)
    {
        // Arrange

        // Act
        var result = EmojiPresentationHelper.GetPreferredTypefaceFallbackCodePoint(textElement);

        // Assert
        Assert.Equal(expectedCodePoint, result);
    }

    [Theory]
    [InlineData("⚠️", 0x26A0)]
    [InlineData("3️⃣", 0x20E3)]
    [InlineData("👨🏻‍🍳", 0x1F468)]
    public void TryGetPreferredEmojiTypefaceFallbackCodePoint_EmojiSequence_ReturnsTrueAndEmojiAnchor(string textElement, int expectedCodePoint)
    {
        // Arrange

        // Act
        var success = EmojiPresentationHelper.TryGetPreferredEmojiTypefaceFallbackCodePoint(textElement, out var codePoint);

        // Assert
        Assert.True(success);
        Assert.Equal(expectedCodePoint, codePoint);
    }

    [Theory]
    [InlineData("⚠︎")]
    [InlineData("A")]
    [InlineData("◽")]
    public void TryGetPreferredEmojiTypefaceFallbackCodePoint_TextPresentationGlyph_ReturnsFalse(string textElement)
    {
        // Arrange

        // Act
        var success = EmojiPresentationHelper.TryGetPreferredEmojiTypefaceFallbackCodePoint(textElement, out var codePoint);

        // Assert
        Assert.False(success);
        Assert.Equal(0, codePoint);
    }
}