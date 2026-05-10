using Mogri.Helpers;
using SkiaSharp;
using Xunit;

namespace Mogri.Tests.Helpers;

public class CanvasTextGeometryHelperTests
{
    [Fact]
    public void GetInflatedBounds_PositivePadding_ExpandsRectSymmetrically()
    {
        // Arrange
        var bounds = new SKRect(10f, 20f, 30f, 40f);

        // Act
        var result = CanvasTextGeometryHelper.GetInflatedBounds(bounds, 5f);

        // Assert
        Assert.Equal(5f, result.Left, 3);
        Assert.Equal(15f, result.Top, 3);
        Assert.Equal(35f, result.Right, 3);
        Assert.Equal(45f, result.Bottom, 3);
    }

    [Fact]
    public void GetLocalTextPoint_NoRotationOrScale_ReturnsBoundsRelativePoint()
    {
        // Arrange
        var bounds = new SKRect(10f, 20f, 30f, 40f);
        var imageLocation = new SKPoint(105f, 210f);

        // Act
        var result = CanvasTextGeometryHelper.GetLocalTextPoint(
            imageLocation,
            100f,
            200f,
            0f,
            1f,
            0.35f,
            bounds);

        // Assert
        Assert.Equal(25f, result.X, 3);
        Assert.Equal(40f, result.Y, 3);
    }

    [Fact]
    public void GetLocalTextPoint_RotationAndScale_InvertsTransform()
    {
        // Arrange
        var bounds = new SKRect(10f, 5f, 30f, 15f);
        var imageLocation = new SKPoint(44f, 80f);

        // Act
        var result = CanvasTextGeometryHelper.GetLocalTextPoint(
            imageLocation,
            50f,
            70f,
            90f,
            2f,
            0.35f,
            bounds);

        // Assert
        Assert.Equal(25f, result.X, 3);
        Assert.Equal(13f, result.Y, 3);
    }

    [Fact]
    public void GetLocalTextPoint_ScaleBelowMinimum_UsesMinimumScale()
    {
        // Arrange
        var bounds = new SKRect(-5f, -5f, 5f, 5f);
        var imageLocation = new SKPoint(13.5f, 20f);

        // Act
        var result = CanvasTextGeometryHelper.GetLocalTextPoint(
            imageLocation,
            10f,
            20f,
            0f,
            0.1f,
            0.35f,
            bounds);

        // Assert
        Assert.Equal(10f, result.X, 3);
        Assert.Equal(0f, result.Y, 3);
    }
}