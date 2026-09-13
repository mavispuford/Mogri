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
        Assert.Equal(original.TextEncoderSecondary, clone.TextEncoderSecondary);
        Assert.Equal(original.EnableTiling, clone.EnableTiling);
        Assert.Equal(original.Seed, clone.Seed);
        Assert.Equal(original.Upscaler, clone.Upscaler);
        Assert.Equal(original.UpscaleLevel, clone.UpscaleLevel);
        Assert.Equal(original.UpscaleSteps, clone.UpscaleSteps);
        Assert.Equal(original.Width, clone.Width);
        Assert.Equal(original.ActualWidth, clone.ActualWidth);
        Assert.Equal(original.ActualHeight, clone.ActualHeight);
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

    [Fact]
    public void NormalizeForBackend_UnsupportedCapabilities_ClearsSettings()
    {
        // Arrange
        var settings = new PromptSettings
        {
            EnableUpscaling = true,
            Upscaler = "4x-UltraSharp.pth",
            UpscaleLevel = 4,
            UpscaleSteps = 20,
            DistilledCfgScale = 3.5,
            EnableTiling = true,
            ModelType = ModelType.Flux
        };
        var capabilities = new BackendCapabilities
        {
            SupportsUpscaling = true,
            SupportsConfigurableUpscaleScale = false,
            SupportsHiresFix = false,
            SupportsDistilledCfgScale = false,
            SupportsSeamless = false
        };

        // Act
        settings.NormalizeForBackend(capabilities);

        // Assert
        Assert.True(settings.EnableUpscaling);
        Assert.Equal("4x-UltraSharp.pth", settings.Upscaler);
        Assert.Equal(0, settings.UpscaleLevel);
        Assert.Equal(0, settings.UpscaleSteps);
        Assert.Null(settings.DistilledCfgScale);
        Assert.False(settings.EnableTiling);
    }

    [Fact]
    public void NormalizeForBackend_ComfyUiModelFamilies_PreservesSupportedShift()
    {
        // Arrange
        var settings = new PromptSettings
        {
            ModelType = ModelType.Flux,
            DistilledCfgScale = 3.5
        };
        var capabilities = new BackendCapabilities
        {
            SupportsDistilledCfgScale = true
        };

        // Act
        settings.NormalizeForBackend(capabilities);

        // Assert
        Assert.Equal(3.5, settings.DistilledCfgScale);
    }

    [Fact]
    public void NormalizeForBackend_IntegratedModel_ClearsExternalResources()
    {
        // Arrange
        var settings = new PromptSettings
        {
            ModelType = ModelType.SDXL,
            Vae = "Qwen2D_VAE.safetensors",
            TextEncoder = "qwen_3_4b.safetensors",
            TextEncoderSecondary = "clip_l.safetensors"
        };

        // Act
        settings.NormalizeForBackend(BackendCapabilities.Full);

        // Assert
        Assert.Null(settings.Vae);
        Assert.Null(settings.TextEncoder);
        Assert.Null(settings.TextEncoderSecondary);
    }

    [Fact]
    public void NormalizeForBackend_ForgeImageToImage_ClearsUnusedSettings()
    {
        // Arrange
        var settings = new PromptSettings
        {
            EnableUpscaling = true,
            Upscaler = "R-ESRGAN 4x+",
            UpscaleLevel = 2,
            UpscaleSteps = 15,
            DistilledCfgScale = 3,
            ModelType = ModelType.SDXL,
            InitImage = "data:image/png;base64,image"
        };

        // Act
        settings.NormalizeForBackend(BackendCapabilities.Full);

        // Assert
        Assert.True(settings.EnableUpscaling);
        Assert.Equal("R-ESRGAN 4x+", settings.Upscaler);
        Assert.Equal(2, settings.UpscaleLevel);
        Assert.Equal(0, settings.UpscaleSteps);
        Assert.Null(settings.DistilledCfgScale);
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
            TextEncoderSecondary = "clip_l",
            EnableTiling = true,
            Seed = 42,
            Upscaler = "ESRGAN",
            UpscaleLevel = 4,
            UpscaleSteps = 15,
            Width = 512,
            ActualWidth = 2048,
            ActualHeight = 3072,
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