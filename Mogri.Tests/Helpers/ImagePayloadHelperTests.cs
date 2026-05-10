using Mogri.Helpers;
using SkiaSharp;
using Xunit;

namespace Mogri.Tests.Helpers;

public class ImagePayloadHelperTests
{
    [Fact]
    public void CreateConstrainedPayload_WithOversizedBitmap_ReturnsEncodedImageAndConstrainedDimensions()
    {
        // Arrange
        using var bitmap = CreateBitmap(3000, 1500, SKColors.CadetBlue);

        // Act
        var payload = ImagePayloadHelper.CreateConstrainedPayload(bitmap);

        // Assert
        Assert.NotNull(payload);
        Assert.Equal(2048d, payload!.Width);
        Assert.Equal(1024d, payload.Height);
        Assert.StartsWith("data:image/png;base64,", payload.ImageDataString);
        Assert.StartsWith("data:image/png;base64,", payload.ThumbnailString);

        using var thumbnailBitmap = DecodeDataString(payload.ThumbnailString);
        Assert.NotNull(thumbnailBitmap);
        Assert.Equal(256, thumbnailBitmap!.Width);
        Assert.Equal(128, thumbnailBitmap.Height);
    }

    [Fact]
    public void CreateFixedPayload_WithBitmap_ReturnsProvidedDimensions()
    {
        // Arrange
        using var bitmap = CreateBitmap(512, 512, SKColors.Orange);

        // Act
        var payload = ImagePayloadHelper.CreateFixedPayload(bitmap, 768d, 768d);

        // Assert
        Assert.NotNull(payload);
        Assert.Equal(768d, payload!.Width);
        Assert.Equal(768d, payload.Height);
        Assert.StartsWith("data:image/png;base64,", payload.ImageDataString);
    }

    [Fact]
    public void HasVisiblePixels_WithTransparentBitmap_ReturnsFalse()
    {
        // Arrange
        using var bitmap = CreateBitmap(2, 2, SKColors.Transparent);

        // Act
        var hasVisiblePixels = ImagePayloadHelper.HasVisiblePixels(bitmap);

        // Assert
        Assert.False(hasVisiblePixels);
    }

    private static SKBitmap CreateBitmap(int width, int height, SKColor fillColor)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(fillColor);

        return bitmap;
    }

    private static SKBitmap? DecodeDataString(string? imageDataString)
    {
        if (string.IsNullOrWhiteSpace(imageDataString))
        {
            return null;
        }

        var match = Constants.ImageDataRegex.Match(imageDataString);
        if (!match.Success)
        {
            return null;
        }

        var bytes = Convert.FromBase64String(match.Groups[Constants.ImageDataCaptureGroupData].Value);
        return SKBitmap.Decode(bytes);
    }
}