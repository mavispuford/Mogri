using Mogri.Helpers;
using SkiaSharp;
using Xunit;

namespace Mogri.Tests.Helpers;

public class ComfyUiMaskHelperTests
{
    [Fact]
    public void CreateSoftMaskPng_WithPositiveBlur_SoftensMaskBoundary()
    {
        // Arrange
        using var sourceBitmap = CreateBitmap(9, 1, SKColors.Transparent);
        sourceBitmap.SetPixel(3, 0, SKColors.Black);
        sourceBitmap.SetPixel(4, 0, SKColors.Black);
        sourceBitmap.SetPixel(5, 0, SKColors.Black);
        var sourceBytes = sourceBitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray();

        // Act
        var resultBytes = ComfyUiMaskHelper.CreateSoftMaskPng(sourceBytes, 2);

        // Assert
        Assert.NotNull(resultBytes);
        using var resultBitmap = SKBitmap.Decode(resultBytes);
        Assert.NotNull(resultBitmap);
        Assert.InRange(resultBitmap.GetPixel(2, 0).Alpha, (byte)1, (byte)254);
        Assert.True(resultBitmap.GetPixel(0, 0).Alpha < resultBitmap.GetPixel(2, 0).Alpha);
        Assert.True(resultBitmap.GetPixel(4, 0).Alpha > resultBitmap.GetPixel(2, 0).Alpha);
    }

    [Fact]
    public void CreateSoftMaskPng_WithZeroBlur_ReturnsOriginalBytes()
    {
        // Arrange
        using var sourceBitmap = CreateBitmap(2, 1, SKColors.Transparent);
        sourceBitmap.SetPixel(1, 0, SKColors.Black);
        var sourceBytes = sourceBitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray();

        // Act
        var resultBytes = ComfyUiMaskHelper.CreateSoftMaskPng(sourceBytes, 0);

        // Assert
        Assert.Same(sourceBytes, resultBytes);
    }

    [Fact]
    public void CreateSoftMaskPng_WithInvalidImage_ReturnsNull()
    {
        // Arrange
        var sourceBytes = new byte[] { 1, 2, 3 };

        // Act
        var resultBytes = ComfyUiMaskHelper.CreateSoftMaskPng(sourceBytes, 2);

        // Assert
        Assert.Null(resultBytes);
    }

    private static SKBitmap CreateBitmap(int width, int height, SKColor fillColor)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(fillColor);

        return bitmap;
    }
}