using Mogri.Models;
using Xunit;

namespace Mogri.Tests.Models;

public class CheckpointSettingsTests
{
    [Fact]
    public void FromPromptSettings_MapsAllProperties()
    {
        // Arrange
        var settings = CreatePromptSettingsForCheckpointMapping();

        // Act
        var checkpoint = CheckpointSettings.FromPromptSettings(settings);

        // Assert
        Assert.Equal(settings.Steps, checkpoint.Steps);
        Assert.Equal(settings.GuidanceScale, checkpoint.GuidanceScale);
        Assert.Equal(settings.DistilledCfgScale, checkpoint.DistilledCfgScale);
        Assert.Equal(settings.Sampler, checkpoint.Sampler);
        Assert.Equal(settings.Scheduler, checkpoint.Scheduler);
        Assert.Equal(settings.Vae, checkpoint.Vae);
        Assert.Equal(settings.TextEncoder, checkpoint.TextEncoder);
        Assert.Equal(settings.Width, checkpoint.Width);
        Assert.Equal(settings.Height, checkpoint.Height);
        Assert.Equal(settings.BatchCount, checkpoint.BatchCount);
        Assert.Equal(settings.BatchSize, checkpoint.BatchSize);
        Assert.Equal(settings.DenoisingStrength, checkpoint.DenoisingStrength);
        Assert.Equal(settings.EnableTiling, checkpoint.EnableTiling);
    }

    [Fact]
    public void FromPromptSettings_NullSampler_DefaultsToEmptyString()
    {
        // Arrange
        var settings = CreatePromptSettingsForCheckpointMapping();
        settings.Sampler = null;

        // Act
        var checkpoint = CheckpointSettings.FromPromptSettings(settings);

        // Assert
        Assert.Equal(string.Empty, checkpoint.Sampler);
    }

    [Fact]
    public void FromPromptSettings_NullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        PromptSettings? settings = null;

        // Act
        var action = () => CheckpointSettings.FromPromptSettings(settings!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void ApplyTo_SetsAllProperties()
    {
        // Arrange
        var checkpoint = CreateCheckpointSettings();
        var destination = new PromptSettings();

        // Act
        checkpoint.ApplyTo(destination);

        // Assert
        Assert.Equal(checkpoint.Steps, destination.Steps);
        Assert.Equal(checkpoint.GuidanceScale, destination.GuidanceScale);
        Assert.Equal(checkpoint.DistilledCfgScale, destination.DistilledCfgScale);
        Assert.Equal(checkpoint.Sampler, destination.Sampler);
        Assert.Equal(checkpoint.Scheduler, destination.Scheduler);
        Assert.Equal(checkpoint.Vae, destination.Vae);
        Assert.Equal(checkpoint.TextEncoder, destination.TextEncoder);
        Assert.Equal(checkpoint.Width, destination.Width);
        Assert.Equal(checkpoint.Height, destination.Height);
        Assert.Equal(checkpoint.BatchCount, destination.BatchCount);
        Assert.Equal(checkpoint.BatchSize, destination.BatchSize);
        Assert.Equal(checkpoint.DenoisingStrength, destination.DenoisingStrength);
        Assert.Equal(checkpoint.EnableTiling, destination.EnableTiling);
    }

    [Fact]
    public void ApplyTo_NullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var checkpoint = CreateCheckpointSettings();
        PromptSettings? settings = null;

        // Act
        var action = () => checkpoint.ApplyTo(settings!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void RoundTrip_FromThenApply_PreservesAllProperties()
    {
        // Arrange
        var source = CreatePromptSettingsForCheckpointMapping();

        // Act
        var checkpoint = CheckpointSettings.FromPromptSettings(source);
        var destination = new PromptSettings();
        checkpoint.ApplyTo(destination);

        // Assert
        Assert.Equal(source.Steps, destination.Steps);
        Assert.Equal(source.GuidanceScale, destination.GuidanceScale);
        Assert.Equal(source.DistilledCfgScale, destination.DistilledCfgScale);
        Assert.Equal(source.Sampler, destination.Sampler);
        Assert.Equal(source.Scheduler, destination.Scheduler);
        Assert.Equal(source.Vae, destination.Vae);
        Assert.Equal(source.TextEncoder, destination.TextEncoder);
        Assert.Equal(source.Width, destination.Width);
        Assert.Equal(source.Height, destination.Height);
        Assert.Equal(source.BatchCount, destination.BatchCount);
        Assert.Equal(source.BatchSize, destination.BatchSize);
        Assert.Equal(source.DenoisingStrength, destination.DenoisingStrength);
        Assert.Equal(source.EnableTiling, destination.EnableTiling);
    }

    private static PromptSettings CreatePromptSettingsForCheckpointMapping()
    {
        return new PromptSettings
        {
            Steps = 20,
            GuidanceScale = 8.0,
            DistilledCfgScale = 4.0,
            Sampler = "Euler a",
            Scheduler = "karras",
            Vae = "auto",
            TextEncoder = "clip",
            Width = 768,
            Height = 512,
            BatchCount = 2,
            BatchSize = 3,
            DenoisingStrength = 0.7,
            EnableTiling = true
        };
    }

    private static CheckpointSettings CreateCheckpointSettings()
    {
        return new CheckpointSettings
        {
            Steps = 21,
            GuidanceScale = 7.2,
            DistilledCfgScale = 3.6,
            Sampler = "DPM++ 2M",
            Scheduler = "karras",
            Vae = "vae.safetensors",
            TextEncoder = "t5xxl",
            Width = 960,
            Height = 640,
            BatchCount = 4,
            BatchSize = 2,
            DenoisingStrength = 0.45,
            EnableTiling = true
        };
    }
}