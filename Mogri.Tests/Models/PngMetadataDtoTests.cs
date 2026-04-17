using Moq;
using Mogri.Enums;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.ViewModels;
using Xunit;

namespace Mogri.Tests.Models;

public class PngMetadataDtoTests
{
    [Fact]
    public void FromPromptSettings_MapsAllFields()
    {
        // Arrange
        var settings = CreatePromptSettingsForMetadata();

        // Act
        var dto = PngMetadataDto.FromPromptSettings(settings);

        // Assert
        Assert.Equal(settings.Prompt, dto.Prompt);
        Assert.Equal(settings.NegativePrompt, dto.NegativePrompt);
        Assert.Equal(settings.Steps, dto.Steps);
        Assert.Equal(settings.Sampler, dto.Sampler);
        Assert.Equal(settings.Scheduler, dto.Scheduler);
        Assert.Equal(settings.GuidanceScale, dto.GuidanceScale);
        Assert.Equal(settings.DistilledCfgScale, dto.DistilledCfgScale);
        Assert.Equal(settings.Seed, dto.Seed);
        Assert.Equal(settings.Width, dto.Width);
        Assert.Equal(settings.Height, dto.Height);
        Assert.Equal(settings.DenoisingStrength, dto.DenoisingStrength);
        Assert.Equal(settings.ModelType.ToString(), dto.ModelType);
        Assert.Equal(settings.Model?.DisplayName, dto.ModelName);
        Assert.Equal(settings.Model?.Key, dto.ModelKey);
        Assert.Equal(settings.EnableUpscaling, dto.EnableUpscaling);
        Assert.Equal(settings.Upscaler, dto.Upscaler);
        Assert.Equal(settings.UpscaleLevel, dto.UpscaleLevel);
        Assert.Equal(settings.UpscaleSteps, dto.UpscaleSteps);
        Assert.Equal(settings.EnableTiling, dto.EnableTiling);

        Assert.NotNull(dto.Loras);
        Assert.Equal(2, dto.Loras.Count);
        Assert.Equal("detail-lora", dto.Loras[0].Name);
        Assert.Equal("detail", dto.Loras[0].Alias);
        Assert.Equal(0.8, dto.Loras[0].Strength);
        Assert.Equal("style-lora", dto.Loras[1].Name);
        Assert.Equal("style", dto.Loras[1].Alias);
        Assert.Equal(1.2, dto.Loras[1].Strength);

        Assert.NotNull(dto.PromptStyleNames);
        Assert.Equal(2, dto.PromptStyleNames.Count);
        Assert.Equal("cinematic", dto.PromptStyleNames[0]);
        Assert.Equal("film-grain", dto.PromptStyleNames[1]);
    }

    [Fact]
    public void ToPromptSettings_MapsAllFields()
    {
        // Arrange
        var dto = new PngMetadataDto
        {
            Prompt = "a fox",
            NegativePrompt = "blurry",
            Steps = 28,
            Sampler = "Euler",
            Scheduler = "karras",
            GuidanceScale = 6.4,
            DistilledCfgScale = 3.3,
            Seed = 987654,
            Width = 896,
            Height = 1152,
            DenoisingStrength = 0.55,
            ModelType = "Flux",
            ModelName = "FluxModel",
            ModelKey = "flux-key",
            EnableUpscaling = true,
            Upscaler = "ESRGAN",
            UpscaleLevel = 4,
            UpscaleSteps = 12,
            EnableTiling = true,
            Loras =
            [
                new PngMetadataDto.LoraEntry("detail-lora", "detail", 0.8),
                new PngMetadataDto.LoraEntry("style-lora", null, 1.2)
            ]
        };

        // Act
        var settings = dto.ToPromptSettings();

        // Assert
        Assert.Equal(dto.Prompt, settings.Prompt);
        Assert.Equal(dto.NegativePrompt, settings.NegativePrompt);
        Assert.Equal(dto.Steps, settings.Steps);
        Assert.Equal(dto.Sampler, settings.Sampler);
        Assert.Equal(dto.Scheduler, settings.Scheduler);
        Assert.Equal(dto.GuidanceScale, settings.GuidanceScale);
        Assert.Equal(dto.DistilledCfgScale, settings.DistilledCfgScale);
        Assert.Equal(dto.Seed, settings.Seed);
        Assert.Equal(dto.Width, settings.Width);
        Assert.Equal(dto.Height, settings.Height);
        Assert.Equal(dto.DenoisingStrength, settings.DenoisingStrength);
        Assert.Equal(ModelType.Flux, settings.ModelType);
        Assert.NotNull(settings.Model);
        Assert.Equal(dto.ModelName, settings.Model.DisplayName);
        Assert.Equal(dto.ModelKey, settings.Model.Key);
        Assert.Equal(dto.EnableUpscaling, settings.EnableUpscaling);
        Assert.Equal(dto.Upscaler, settings.Upscaler);
        Assert.Equal(dto.UpscaleLevel, settings.UpscaleLevel);
        Assert.Equal(dto.UpscaleSteps, settings.UpscaleSteps);
        Assert.Equal(dto.EnableTiling, settings.EnableTiling);

        Assert.Equal(2, settings.Loras.Count);
        Assert.Equal("detail-lora", settings.Loras[0].Name);
        Assert.Equal("detail", settings.Loras[0].Alias);
        Assert.Equal(0.8, settings.Loras[0].Strength);
        Assert.Equal("style-lora", settings.Loras[1].Name);
        Assert.Equal(string.Empty, settings.Loras[1].Alias);
        Assert.Equal(1.2, settings.Loras[1].Strength);
    }

    [Fact]
    public void RoundTrip_FromThenTo_PreservesMappedData()
    {
        // Arrange
        var original = CreatePromptSettingsForMetadata();

        // Act
        var dto = PngMetadataDto.FromPromptSettings(original);
        var roundTripped = dto.ToPromptSettings();

        // Assert
        Assert.Equal(original.Prompt, roundTripped.Prompt);
        Assert.Equal(original.NegativePrompt, roundTripped.NegativePrompt);
        Assert.Equal(original.Steps, roundTripped.Steps);
        Assert.Equal(original.Sampler, roundTripped.Sampler);
        Assert.Equal(original.Scheduler, roundTripped.Scheduler);
        Assert.Equal(original.GuidanceScale, roundTripped.GuidanceScale);
        Assert.Equal(original.DistilledCfgScale, roundTripped.DistilledCfgScale);
        Assert.Equal(original.Seed, roundTripped.Seed);
        Assert.Equal(original.Width, roundTripped.Width);
        Assert.Equal(original.Height, roundTripped.Height);
        Assert.Equal(original.DenoisingStrength, roundTripped.DenoisingStrength);
        Assert.Equal(original.ModelType, roundTripped.ModelType);
        Assert.NotNull(roundTripped.Model);
        Assert.Equal(original.Model?.DisplayName, roundTripped.Model.DisplayName);
        Assert.Equal(original.Model?.Key, roundTripped.Model.Key);
        Assert.Equal(original.EnableUpscaling, roundTripped.EnableUpscaling);
        Assert.Equal(original.Upscaler, roundTripped.Upscaler);
        Assert.Equal(original.UpscaleLevel, roundTripped.UpscaleLevel);
        Assert.Equal(original.UpscaleSteps, roundTripped.UpscaleSteps);
        Assert.Equal(original.EnableTiling, roundTripped.EnableTiling);

        Assert.Equal(2, roundTripped.Loras.Count);
        Assert.Equal("detail-lora", roundTripped.Loras[0].Name);
        Assert.Equal("style-lora", roundTripped.Loras[1].Name);
    }

    [Fact]
    public void FromPromptSettings_NullModel_OmitsModelFields()
    {
        // Arrange
        var settings = CreatePromptSettingsForMetadata();
        settings.Model = null;

        // Act
        var dto = PngMetadataDto.FromPromptSettings(settings);

        // Assert
        Assert.Null(dto.ModelName);
        Assert.Null(dto.ModelKey);
    }

    [Fact]
    public void ToPromptSettings_InvalidModelType_UsesDefault()
    {
        // Arrange
        var dto = new PngMetadataDto
        {
            Prompt = "a prompt",
            ModelType = "InvalidValue"
        };

        // Act
        var settings = dto.ToPromptSettings();

        // Assert
        Assert.Equal(ModelType.SDXL, settings.ModelType);
    }

    [Fact]
    public void ToPromptSettings_NullLoras_LorasListEmpty()
    {
        // Arrange
        var dto = new PngMetadataDto
        {
            Prompt = "a prompt",
            ModelType = ModelType.SDXL.ToString(),
            Loras = null
        };

        // Act
        var settings = dto.ToPromptSettings();

        // Assert
        Assert.NotNull(settings.Loras);
        Assert.Empty(settings.Loras);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ToPromptSettings_EmptyModelName_NoModelCreated(string? modelName)
    {
        // Arrange
        var dto = new PngMetadataDto
        {
            Prompt = "a prompt",
            ModelType = ModelType.Flux.ToString(),
            ModelName = modelName,
            ModelKey = "model-key"
        };

        // Act
        var settings = dto.ToPromptSettings();

        // Assert
        Assert.Null(settings.Model);
    }

    private static PromptSettings CreatePromptSettingsForMetadata()
    {
        return new PromptSettings
        {
            Prompt = "a cat",
            NegativePrompt = "blur",
            Steps = 20,
            Sampler = "Euler a",
            Scheduler = "karras",
            GuidanceScale = 8.0,
            DistilledCfgScale = 4.0,
            Seed = 42,
            Width = 512,
            Height = 768,
            DenoisingStrength = 0.7,
            ModelType = ModelType.Flux,
            Model = new ModelViewModel
            {
                DisplayName = "TestModel",
                Key = "model-key"
            },
            EnableUpscaling = true,
            Upscaler = "ESRGAN",
            UpscaleLevel = 4,
            UpscaleSteps = 15,
            EnableTiling = true,
            Loras =
            [
                new LoraViewModel
                {
                    Name = "detail-lora",
                    Alias = "detail",
                    Strength = 0.8
                },
                new LoraViewModel
                {
                    Name = "style-lora",
                    Alias = "style",
                    Strength = 1.2
                }
            ],
            PromptStyles =
            [
                CreatePromptStyle("cinematic"),
                CreatePromptStyle("film-grain")
            ]
        };
    }

    private static IPromptStyleViewModel CreatePromptStyle(string name)
    {
        var style = new Mock<IPromptStyleViewModel>();
        style.SetupGet(x => x.Name).Returns(name);
        return style.Object;
    }
}