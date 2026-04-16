using Mogri.Services;
using Xunit;

namespace Mogri.Tests.Services;

public class ForgeMetadataParserTests
{
    [Fact]
    public void Parse_NullInput_ReturnsDefaultPromptSettings()
    {
        var result = ForgeMetadataParser.Parse(null!);

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Prompt);
        Assert.Equal(string.Empty, result.NegativePrompt);
        Assert.Equal(30, result.Steps);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsDefaultPromptSettings()
    {
        var result = ForgeMetadataParser.Parse(string.Empty);

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Prompt);
        Assert.Equal(string.Empty, result.NegativePrompt);
    }

    [Fact]
    public void Parse_SingleLineOnly_ReturnsNull()
    {
        var result = ForgeMetadataParser.Parse("single line only");

        Assert.Null(result);
    }

    [Fact]
    public void Parse_FullMetadata_ExtractsAllFields()
    {
        const string info = "a photo of a cat, detailed fur\n" +
            "Negative prompt: blur, noise\n" +
            "Steps: 30, Sampler: DPM++ 2M, CFG scale: 7.0, Seed: 12345, Size: 1024x1024, Denoising strength: 0.5";

        var result = ForgeMetadataParser.Parse(info);

        Assert.NotNull(result);
        Assert.Equal("a photo of a cat, detailed fur", result.Prompt);
        Assert.Equal("blur, noise", result.NegativePrompt);
        Assert.Equal(30, result.Steps);
        Assert.Equal("DPM++ 2M", result.Sampler);
        Assert.Equal(7.0, result.GuidanceScale);
        Assert.Equal(12345, result.Seed);
        Assert.Equal(1024, result.Width);
        Assert.Equal(1024, result.Height);
        Assert.Equal(0.5, result.DenoisingStrength);
    }

    [Fact]
    public void Parse_WithUpscaleInfo_ExtractsUpscaleFields()
    {
        const string info = "a cat\n" +
            "Negative prompt: blur\n" +
            "Steps: 20, Seed: 1, Hires upscaler: R-ESRGAN 4x+, Hires upscale: 2, Hires steps: 15";

        var result = ForgeMetadataParser.Parse(info);

        Assert.NotNull(result);
        Assert.True(result.EnableUpscaling);
        Assert.Equal("R-ESRGAN 4x+", result.Upscaler);
        Assert.Equal(2, result.UpscaleLevel);
        Assert.Equal(15, result.UpscaleSteps);
    }

    [Fact]
    public void Parse_WithScheduler_ExtractsScheduler()
    {
        const string info = "a cat\n" +
            "Negative prompt: blur\n" +
            "Steps: 20, Schedule type: karras";

        var result = ForgeMetadataParser.Parse(info);

        Assert.NotNull(result);
        Assert.Equal("karras", result.Scheduler);
    }

    [Fact]
    public void Parse_WithDistilledCfg_ExtractsDistilledCfg()
    {
        const string info = "a cat\n" +
            "Negative prompt: blur\n" +
            "Steps: 20, Distilled CFG Scale: 3.5";

        var result = ForgeMetadataParser.Parse(info);

        Assert.NotNull(result);
        Assert.Equal(3.5, result.DistilledCfgScale);
    }

    [Fact]
    public void Parse_WithModelAndModelHash_ExtractsModelIdentity()
    {
        const string info = "a cat\n" +
            "Negative prompt: blur\n" +
            "Steps: 20, Model: juggernautXL.safetensors, Model hash: abc123";

        var result = ForgeMetadataParser.Parse(info);

        Assert.NotNull(result);
        Assert.NotNull(result.Model);
        Assert.Equal("juggernautXL.safetensors", result.Model.DisplayName);
        Assert.Equal("abc123", result.Model.Key);
    }

    [Fact]
    public void Parse_WithOnlyModel_UsesModelAsFallbackKey()
    {
        const string info = "a cat\n" +
            "Negative prompt: blur\n" +
            "Steps: 20, Model: dreamshaper.safetensors";

        var result = ForgeMetadataParser.Parse(info);

        Assert.NotNull(result);
        Assert.NotNull(result.Model);
        Assert.Equal("dreamshaper.safetensors", result.Model.DisplayName);
        Assert.Equal("dreamshaper.safetensors", result.Model.Key);
    }

    [Fact]
    public void Parse_PromptWithLoras_ExtractsLorasAndCleansPrompt()
    {
        const string info = "a cat <lora:myLora:0.8>, cinematic\n" +
            "Negative prompt: blur\n" +
            "Steps: 20, Seed: 1";

        var result = ForgeMetadataParser.Parse(info);

        Assert.NotNull(result);
        Assert.Single(result.Loras);
        Assert.Equal("myLora", result.Loras[0].Name);
        Assert.Equal(0.8, result.Loras[0].Strength, 3);
        Assert.DoesNotContain("<lora:", result.Prompt);
    }

    [Fact]
    public void Parse_MultipleLorasInPrompt_ExtractsAll()
    {
        const string info = "<lora:first:0.5> a cat <lora:second:1.1> <lora:first:0.7>\n" +
            "Negative prompt: blur\n" +
            "Steps: 20, Seed: 1";

        var result = ForgeMetadataParser.Parse(info);

        Assert.NotNull(result);
        Assert.Equal(2, result.Loras.Count);
        Assert.Equal("first", result.Loras[0].Name);
        Assert.Equal("second", result.Loras[1].Name);
    }

    [Fact]
    public void Parse_MissingFields_SetsDefaults()
    {
        const string info = "a cat\n" +
            "Negative prompt: blur\n" +
            "Steps: 45, Seed: 321";

        var result = ForgeMetadataParser.Parse(info);

        Assert.NotNull(result);
        Assert.Equal(45, result.Steps);
        Assert.Equal(321, result.Seed);
        Assert.Equal(7.5, result.GuidanceScale);
        Assert.Equal(1024, result.Width);
        Assert.Equal(1024, result.Height);
        Assert.Null(result.Sampler);
    }

    [Fact]
    public void Parse_MalformedPropertyValue_SkipsGracefully()
    {
        const string info = "a cat\n" +
            "Negative prompt: blur\n" +
            "Steps: abc, CFG scale: invalid, Seed: nope, Size: badxdata";

        var result = ForgeMetadataParser.Parse(info);

        Assert.NotNull(result);
        Assert.Equal(30, result.Steps);
        Assert.Equal(7.5, result.GuidanceScale);
        Assert.Equal(-1, result.Seed);
        Assert.Equal(1024, result.Width);
        Assert.Equal(1024, result.Height);
    }
}
