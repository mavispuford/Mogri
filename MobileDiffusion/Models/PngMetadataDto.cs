using System.Text.Json.Serialization;
using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.ViewModels;

namespace MobileDiffusion.Models;

/// <summary>
/// Data Transfer Object for serializing generation metadata into PNG chunks.
/// This structure is designed to be compact and self-contained, omitting
/// runtime-specific or large binary fields found in PromptSettings.
/// </summary>
public class PngMetadataDto
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("negativePrompt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string NegativePrompt { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public int Steps { get; set; }

    [JsonPropertyName("sampler")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sampler { get; set; }

    [JsonPropertyName("scheduler")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scheduler { get; set; }

    [JsonPropertyName("cfg")]
    public double GuidanceScale { get; set; }

    [JsonPropertyName("distilledCfg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? DistilledCfgScale { get; set; }

    [JsonPropertyName("seed")]
    public long Seed { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }

    [JsonPropertyName("denoising")]
    public double DenoisingStrength { get; set; }

    [JsonPropertyName("modelType")]
    public string ModelType { get; set; } = string.Empty;

    [JsonPropertyName("modelName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModelName { get; set; }

    [JsonPropertyName("modelKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModelKey { get; set; }

    [JsonPropertyName("enableUpscaling")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableUpscaling { get; set; }

    [JsonPropertyName("upscaler")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Upscaler { get; set; }

    [JsonPropertyName("upscaleLevel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int UpscaleLevel { get; set; }

    [JsonPropertyName("upscaleSteps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int UpscaleSteps { get; set; }

    [JsonPropertyName("enableFaceRestoration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableFaceRestoration { get; set; }

    [JsonPropertyName("faceRestorationStrength")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double FaceRestorationStrength { get; set; }

    [JsonPropertyName("enableTiling")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableTiling { get; set; }

    [JsonPropertyName("loras")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LoraEntry>? Loras { get; set; }

    [JsonPropertyName("styles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? PromptStyleNames { get; set; }

    public record LoraEntry(string Name, string? Alias, double Strength);

    public static PngMetadataDto FromPromptSettings(PromptSettings settings)
    {
        return new PngMetadataDto
        {
            Prompt = settings.Prompt,
            NegativePrompt = settings.NegativePrompt,
            Steps = settings.Steps,
            Sampler = settings.Sampler,
            Scheduler = settings.Scheduler,
            GuidanceScale = settings.GuidanceScale,
            DistilledCfgScale = settings.DistilledCfgScale,
            Seed = settings.Seed,
            Width = settings.Width,
            Height = settings.Height,
            DenoisingStrength = settings.DenoisingStrength,
            ModelType = settings.ModelType.ToString(),
            ModelName = settings.Model?.DisplayName,
            ModelKey = settings.Model?.Key,
            EnableUpscaling = settings.EnableUpscaling,
            Upscaler = settings.Upscaler,
            UpscaleLevel = settings.UpscaleLevel,
            UpscaleSteps = settings.UpscaleSteps,
            EnableFaceRestoration = settings.EnableFaceRestoration,
            FaceRestorationStrength = settings.FaceRestorationStrength,
            EnableTiling = settings.EnableTiling,
            Loras = settings.Loras?.Select(l => new LoraEntry(l.Name, l.Alias, l.Strength)).ToList(),
            PromptStyleNames = settings.PromptStyles?.Select(s => s.Name).ToList()
        };
    }

    public PromptSettings ToPromptSettings()
    {
        var settings = new PromptSettings
        {
            Prompt = this.Prompt,
            NegativePrompt = this.NegativePrompt,
            Steps = this.Steps,
            Sampler = this.Sampler,
            Scheduler = this.Scheduler,
            GuidanceScale = this.GuidanceScale,
            DistilledCfgScale = this.DistilledCfgScale,
            Seed = this.Seed,
            Width = this.Width,
            Height = this.Height,
            DenoisingStrength = this.DenoisingStrength,
            EnableUpscaling = this.EnableUpscaling,
            Upscaler = this.Upscaler,
            UpscaleLevel = this.UpscaleLevel,
            UpscaleSteps = this.UpscaleSteps,
            EnableFaceRestoration = this.EnableFaceRestoration,
            FaceRestorationStrength = this.FaceRestorationStrength,
            EnableTiling = this.EnableTiling
        };

        if (Enum.TryParse<ModelType>(this.ModelType, out var modelType))
        {
            settings.ModelType = modelType;
        }

        if (!string.IsNullOrEmpty(this.ModelName))
        {
            settings.Model = new ModelViewModel
            {
                DisplayName = this.ModelName,
                Key = this.ModelKey ?? string.Empty
            };
        }

        if (this.Loras != null)
        {
            foreach (var lora in this.Loras)
            {
                settings.Loras.Add(new LoraViewModel
                {
                    Name = lora.Name,
                    Alias = lora.Alias ?? string.Empty,
                    Strength = lora.Strength
                });
            }
        }
        
        return settings;
    }
}
