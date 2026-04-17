using Moq;
using Mogri.Enums;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.ViewModels;
using Xunit;

namespace Mogri.Tests.Models;

public class PromptSettingsTests
{
    [Fact]
    public void Clone_AllScalarPropertiesCopied()
    {
        // Arrange
        var original = CreatePopulatedPromptSettings();

        // Act
        var clone = original.Clone();

        // Assert
        Assert.Equal(original.EnableUpscaling, clone.EnableUpscaling);
        Assert.Equal(original.EnableFitServerSide, clone.EnableFitServerSide);
        Assert.Equal(original.FitClientSide, clone.FitClientSide);
        Assert.Equal(original.GuidanceScale, clone.GuidanceScale);
        Assert.Equal(original.DistilledCfgScale, clone.DistilledCfgScale);
        Assert.Equal(original.Height, clone.Height);
        Assert.Equal(original.InitImage, clone.InitImage);
        Assert.Equal(original.InitImageThumbnail, clone.InitImageThumbnail);
        Assert.Equal(original.Mask, clone.Mask);
        Assert.Equal(original.MaskBlur, clone.MaskBlur);
        Assert.Equal(original.ModelType, clone.ModelType);
        Assert.Equal(original.Steps, clone.Steps);
        Assert.Equal(original.BatchCount, clone.BatchCount);
        Assert.Equal(original.BatchSize, clone.BatchSize);
        Assert.Equal(original.Prompt, clone.Prompt);
        Assert.Equal(original.NegativePrompt, clone.NegativePrompt);
        Assert.Equal(original.DenoisingStrength, clone.DenoisingStrength);
        Assert.Equal(original.Sampler, clone.Sampler);
        Assert.Equal(original.Scheduler, clone.Scheduler);
        Assert.Equal(original.Vae, clone.Vae);
        Assert.Equal(original.TextEncoder, clone.TextEncoder);
        Assert.Equal(original.EnableTiling, clone.EnableTiling);
        Assert.Equal(original.Seed, clone.Seed);
        Assert.Equal(original.Upscaler, clone.Upscaler);
        Assert.Equal(original.UpscaleLevel, clone.UpscaleLevel);
        Assert.Equal(original.UpscaleSteps, clone.UpscaleSteps);
        Assert.Equal(original.Width, clone.Width);
    }

    [Fact]
    public void Clone_LoraListIsIndependentCopy()
    {
        // Arrange
        var original = CreatePopulatedPromptSettings();

        // Act
        var clone = original.Clone();
        clone.Loras.Add(new LoraViewModel
        {
            Name = "second-lora",
            Alias = "second",
            Strength = 1.1
        });

        // Assert
        Assert.NotSame(original.Loras, clone.Loras);
        Assert.Single(original.Loras);
        Assert.Equal(2, clone.Loras.Count);
    }

    [Fact]
    public void Clone_PromptStyleListIsIndependentCopy()
    {
        // Arrange
        var original = CreatePopulatedPromptSettings();

        // Act
        var clone = original.Clone();
        clone.PromptStyles.Add(CreatePromptStyle("noir"));

        // Assert
        Assert.NotSame(original.PromptStyles, clone.PromptStyles);
        Assert.Single(original.PromptStyles);
        Assert.Equal(2, clone.PromptStyles.Count);
    }

    [Fact]
    public void Clone_ModifyCloneDoesNotAffectOriginal()
    {
        // Arrange
        var original = CreatePopulatedPromptSettings();

        // Act
        var clone = original.Clone();
        clone.Prompt = "different prompt";
        clone.Steps = 99;
        clone.Seed = 123456;

        // Assert
        Assert.Equal("a cat", original.Prompt);
        Assert.Equal(20, original.Steps);
        Assert.Equal(42, original.Seed);
    }

    [Fact]
    public void Clone_ModelReferenceIsShared()
    {
        // Arrange
        var original = CreatePopulatedPromptSettings();

        // Act
        var clone = original.Clone();

        // Assert
        Assert.Same(original.Model, clone.Model);
    }

    private static PromptSettings CreatePopulatedPromptSettings()
    {
        return new PromptSettings
        {
            EnableUpscaling = true,
            EnableFitServerSide = false,
            FitClientSide = false,
            GuidanceScale = 8.0,
            DistilledCfgScale = 4.0,
            Height = 768,
            InitImage = "base64img",
            InitImageThumbnail = "base64thumb",
            Mask = "base64mask",
            MaskBlur = 10,
            ModelType = ModelType.Flux,
            Steps = 20,
            BatchCount = 2,
            BatchSize = 3,
            Prompt = "a cat",
            NegativePrompt = "blur",
            DenoisingStrength = 0.7,
            Model = new ModelViewModel
            {
                DisplayName = "TestModel",
                Key = "model-key"
            },
            Sampler = "Euler a",
            Scheduler = "karras",
            Vae = "auto",
            TextEncoder = "clip",
            EnableTiling = true,
            Seed = 42,
            Upscaler = "ESRGAN",
            UpscaleLevel = 4,
            UpscaleSteps = 15,
            Width = 512,
            Loras =
            [
                new LoraViewModel
                {
                    Name = "detail-lora",
                    Alias = "detail",
                    Strength = 0.8
                }
            ],
            PromptStyles = [CreatePromptStyle("cinematic")]
        };
    }

    private static IPromptStyleViewModel CreatePromptStyle(string name)
    {
        var style = new Mock<IPromptStyleViewModel>();
        style.SetupGet(x => x.Name).Returns(name);
        return style.Object;
    }
}