using Mogri.Enums;
using Mogri.Models;
using Xunit;

namespace Mogri.Tests.Models;

public class GenerationProfileTests
{
    [Theory]
    [InlineData(ModelType.SD15, 30, 6.0, null, "DPM++ 2M", "karras", 512, 512)]
    [InlineData(ModelType.SDXL, 30, 6.0, null, "DPM++ 2M", "karras", 1024, 1024)]
    [InlineData(ModelType.Flux, 20, 1.0, 3.5, "Euler", "beta", 1024, 1024)]
    [InlineData(ModelType.ZImageTurbo, 8, 1.0, 3.5, "Euler", "beta", 1024, 1024)]
    public void GetDefault_ReturnsExpectedProfile(
        ModelType modelType,
        int expectedSteps,
        double expectedCfg,
        double? expectedDistilledCfg,
        string expectedSampler,
        string expectedScheduler,
        double expectedWidth,
        double expectedHeight)
    {
        // Arrange

        // Act
        var profile = GenerationProfile.GetDefault(modelType);

        // Assert
        Assert.Equal(expectedSteps, profile.DefaultSteps);
        Assert.Equal(expectedCfg, profile.DefaultCfg);
        Assert.Equal(expectedDistilledCfg, profile.DefaultDistilledCfg);
        Assert.Equal(expectedSampler, profile.DefaultSampler);
        Assert.Equal(expectedScheduler, profile.DefaultScheduler);
        Assert.Equal(expectedWidth, profile.DefaultWidth);
        Assert.Equal(expectedHeight, profile.DefaultHeight);
    }

    [Fact]
    public void GetDefault_ZImageTurbo_HasVaeAndTextEncoder()
    {
        // Arrange

        // Act
        var profile = GenerationProfile.GetDefault(ModelType.ZImageTurbo);

        // Assert
        Assert.Equal("ae.safetensors", profile.DefaultVae);
        Assert.Equal("Qwen3", profile.DefaultTextEncoder);
    }

    [Fact]
    public void GetDefault_Flux_HasVaeAndTextEncoder()
    {
        // Arrange

        // Act
        var profile = GenerationProfile.GetDefault(ModelType.Flux);

        // Assert
        Assert.Equal("ae.safetensors", profile.DefaultVae);
        Assert.Equal("t5xxl", profile.DefaultTextEncoder);
    }

    [Fact]
    public void GetDefault_SD15_NoVaeOrTextEncoder()
    {
        // Arrange

        // Act
        var profile = GenerationProfile.GetDefault(ModelType.SD15);

        // Assert
        Assert.Null(profile.DefaultVae);
        Assert.Null(profile.DefaultTextEncoder);
    }

    [Fact]
    public void GetDefault_UnknownEnumValue_FallsToSdxlDefaults()
    {
        // Arrange
        var unknownModelType = (ModelType)999;

        // Act
        var profile = GenerationProfile.GetDefault(unknownModelType);

        // Assert
        Assert.Equal(30, profile.DefaultSteps);
        Assert.Equal(6.0, profile.DefaultCfg);
        Assert.Null(profile.DefaultDistilledCfg);
        Assert.Equal("DPM++ 2M", profile.DefaultSampler);
        Assert.Equal("karras", profile.DefaultScheduler);
        Assert.Null(profile.DefaultVae);
        Assert.Null(profile.DefaultTextEncoder);
        Assert.Equal(1024, profile.DefaultWidth);
        Assert.Equal(1024, profile.DefaultHeight);
    }
}