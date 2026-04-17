using Mogri.Helpers;
using Xunit;

namespace Mogri.Tests.Helpers;

public class MathHelperTests
{
    [Theory]
    [InlineData(12, 8, 4)]
    [InlineData(1920, 1080, 120)]
    [InlineData(7, 13, 1)]
    [InlineData(0, 5, 5)]
    [InlineData(100, 100, 100)]
    public void GreatestCommonDivisor_ReturnsExpectedValues(int a, int b, int expected)
    {
        var result = MathHelper.GreatestCommonDivisor(a, b);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1920, 1080, "16:9", 16, 9)]
    [InlineData(1024, 1024, "1:1", 1, 1)]
    public void CalculateAspectRatio_ValidDimensions_ReturnsExpectedValues(
        double width,
        double height,
        string expectedAspectRatio,
        int expectedAspectWidth,
        int expectedAspectHeight)
    {
        var result = MathHelper.CalculateAspectRatio(width, height);

        Assert.Equal(expectedAspectRatio, result.AspectRatioString);
        Assert.Equal(expectedAspectWidth, result.AspectWidth);
        Assert.Equal(expectedAspectHeight, result.AspectHeight);
    }

    [Fact]
    public void CalculateAspectRatio_ZeroWidth_ReturnsEmptyResult()
    {
        var result = MathHelper.CalculateAspectRatio(0, 1080);

        Assert.Equal(0, result.AspectRatioDouble);
        Assert.Equal(string.Empty, result.AspectRatioString);
        Assert.Equal(0, result.Gcd);
        Assert.Equal(0, result.AspectWidth);
        Assert.Equal(0, result.AspectHeight);
    }

    [Fact]
    public void CalculateAspectRatio_NegativeWidth_ReturnsEmptyResult()
    {
        var result = MathHelper.CalculateAspectRatio(-1, 1080);

        Assert.Equal(0, result.AspectRatioDouble);
        Assert.Equal(string.Empty, result.AspectRatioString);
        Assert.Equal(0, result.Gcd);
        Assert.Equal(0, result.AspectWidth);
        Assert.Equal(0, result.AspectHeight);
    }

    [Theory]
    [InlineData(1024, 1024)]
    [InlineData(1023, 1024)]
    [InlineData(50, 64)]
    [InlineData(3000, 2048)]
    [InlineData(69, 72)]
    public void ConstrainDimensionValue_ReturnsExpectedValue(double input, double expected)
    {
        var result = MathHelper.ConstrainDimensionValue(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetAspectCorrectConstrainedDimensions_UseMaximumWidthHeight_LandscapeSetsWidthToMax()
    {
        var result = MathHelper.GetAspectCorrectConstrainedDimensions(
            1920,
            1080,
            dimensionConstraint: MathHelper.DimensionConstraint.UseMaximumWidthHeight);

        Assert.Equal(Constants.MaximumWidthHeight, result.Width);
    }

    [Fact]
    public void GetAspectCorrectConstrainedDimensions_UseMinimumWidthHeight_PortraitSetsWidthToMin()
    {
        var result = MathHelper.GetAspectCorrectConstrainedDimensions(
            1080,
            1920,
            dimensionConstraint: MathHelper.DimensionConstraint.UseMinimumWidthHeight);

        Assert.Equal(Constants.MinimumWidthHeight, result.Width);
    }

    [Fact]
    public void GetAspectCorrectConstrainedDimensions_NegativeInput_ReturnsZeroes()
    {
        var result = MathHelper.GetAspectCorrectConstrainedDimensions(
            -1,
            1080,
            dimensionConstraint: MathHelper.DimensionConstraint.ClosestMatch);

        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
    }

    [Fact]
    public void GetAspectCorrectConstrainedDimensions_ClosestMatch_InRangeDimensionsStayConstrained()
    {
        var result = MathHelper.GetAspectCorrectConstrainedDimensions(
            1024,
            768,
            dimensionConstraint: MathHelper.DimensionConstraint.ClosestMatch);

        Assert.Equal(1024, result.Width);
        Assert.Equal(768, result.Height);
    }

    [Fact]
    public void GetAspectCorrectConstrainedDimensions_ResultAlwaysUsesResolutionDivisor()
    {
        var result = MathHelper.GetAspectCorrectConstrainedDimensions(
            1001,
            777,
            dimensionConstraint: MathHelper.DimensionConstraint.UseMaximumWidthHeight);

        Assert.Equal(0, result.Width % Constants.ResolutionValueDivisor);
        Assert.Equal(0, result.Height % Constants.ResolutionValueDivisor);
    }
}
