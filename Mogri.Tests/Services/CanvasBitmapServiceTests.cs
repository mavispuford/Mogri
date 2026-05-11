using Mogri.Interfaces.Services;
using Mogri.Services;
using SkiaSharp;
using Xunit;

namespace Mogri.Tests.Services;

public class CanvasBitmapServiceTests
{
    [Fact]
    public void CreateBlackAndWhiteMask_WithOpaquePixels_ReturnsOpaqueBlackMask()
    {
        // Arrange
        using var bitmap = CreateBitmap(2, 1, SKColors.Transparent);
        bitmap.SetPixel(1, 0, new SKColor(20, 40, 60, 128));
        var service = CreateService();

        // Act
        using var result = service.CreateBlackAndWhiteMask(bitmap);

        // Assert
        Assert.NotNull(result);
        Assert.Equal((byte)0, result!.GetPixel(0, 0).Alpha);
        Assert.Equal(SKColors.Black, result.GetPixel(1, 0));
    }

    [Fact]
    public void CreateMaskBitmapFromSegmentationMask_WithOpaquePixel_ReturnsOpaqueWhitePixel()
    {
        // Arrange
        using var bitmap = CreateBitmap(1, 1, SKColors.Red);
        var service = CreateService();

        // Act
        using var result = service.CreateMaskBitmapFromSegmentationMask(bitmap);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SKColors.White, result!.GetPixel(0, 0));
    }

    [Fact]
    public void CreateMaskedBitmap_WithOpaqueMask_OverlaysMaskOnSource()
    {
        // Arrange
        using var sourceBitmap = CreateBitmap(1, 1, SKColors.White);
        using var maskBitmap = CreateBitmap(1, 1, SKColors.Black);
        var service = CreateService();

        // Act
        using var result = service.CreateMaskedBitmap(sourceBitmap, maskBitmap);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SKColors.Black, result!.GetPixel(0, 0));
    }

    [Fact]
    public void GetCroppedBitmap_WithValidRect_ReturnsExpectedSection()
    {
        // Arrange
        using var bitmap = CreateBitmap(4, 4, SKColors.Transparent);
        bitmap.SetPixel(1, 1, SKColors.Red);
        bitmap.SetPixel(2, 1, SKColors.Green);
        bitmap.SetPixel(1, 2, SKColors.Blue);
        bitmap.SetPixel(2, 2, SKColors.Yellow);
        var service = CreateService();

        // Act
        using var result = service.GetCroppedBitmap(bitmap, new SKRect(1, 1, 3, 3), 1d, 2f);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SKColors.Red, result!.GetPixel(0, 0));
        Assert.Equal(SKColors.Green, result.GetPixel(1, 0));
        Assert.Equal(SKColors.Blue, result.GetPixel(0, 1));
        Assert.Equal(SKColors.Yellow, result.GetPixel(1, 1));
    }

    [Fact]
    public void GetCroppedBitmap_WithZeroCropSize_ReturnsOriginalBitmapReference()
    {
        // Arrange
        using var bitmap = CreateBitmap(4, 4, SKColors.CadetBlue);
        var service = CreateService();

        // Act
        var result = service.GetCroppedBitmap(bitmap, new SKRect(1, 1, 3, 3), 1d, 0f);

        // Assert
        Assert.Same(bitmap, result);
    }

    [Fact]
    public void StitchBitmapIntoSource_WithTargetRect_DrawsInsertedBitmapInRect()
    {
        // Arrange
        using var sourceBitmap = CreateBitmap(4, 4, SKColors.White);
        using var insertBitmap = CreateBitmap(2, 2, SKColors.Black);
        var service = CreateService();

        // Act
        using var result = service.StitchBitmapIntoSource(sourceBitmap, insertBitmap, new SKRect(1, 1, 3, 3), 1d);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SKColors.White, result!.GetPixel(0, 0));
        Assert.Equal(SKColors.Black, result.GetPixel(1, 1));
        Assert.Equal(SKColors.Black, result.GetPixel(2, 2));
    }

    private static ICanvasBitmapService CreateService()
    {
        return new CanvasBitmapService();
    }

    private static SKBitmap CreateBitmap(int width, int height, SKColor fillColor)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(fillColor);

        return bitmap;
    }
}