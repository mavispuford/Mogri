using Mogri.Helpers;
using Xunit;

namespace Mogri.Tests.Helpers;

public class ComfyUiSamplingHelperTests
{
    [Theory]
    [InlineData("Euler a", "euler_ancestral")]
    [InlineData("euler", "euler")]
    [InlineData("DPM++ 2M", "dpmpp_2m")]
    [InlineData("DPM++ SDE", "dpmpp_sde")]
    public void FindSampler_WithForgeLabel_ReturnsComfyName(string requestedSampler, string expectedSampler)
    {
        // Arrange
        var availableSamplers = new[] { "euler", "euler_ancestral", "dpmpp_2m", "dpmpp_sde" };

        // Act
        var result = ComfyUiSamplingHelper.FindSampler(availableSamplers, requestedSampler);

        // Assert
        Assert.Equal(expectedSampler, result);
    }

    [Fact]
    public void FindSampler_WithUnsupportedSampler_ReturnsNull()
    {
        // Arrange
        var availableSamplers = new[] { "euler", "euler_ancestral" };

        // Act
        var result = ComfyUiSamplingHelper.FindSampler(availableSamplers, "unknown");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindScheduler_WithCaseDifference_ReturnsServerName()
    {
        // Arrange
        var availableSchedulers = new[] { "simple", "beta", "normal" };

        // Act
        var result = ComfyUiSamplingHelper.FindScheduler(availableSchedulers, "Beta");

        // Assert
        Assert.Equal("beta", result);
    }
}