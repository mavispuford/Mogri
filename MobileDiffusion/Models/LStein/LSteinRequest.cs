using System.Text.Json.Serialization;

namespace MobileDiffusion.Models.LStein;

public class LSteinRequest
{
    private static Random random = new Random();
    
    [JsonPropertyName("cfg_scale")]
    public string CfgScale { get; set; }
    
    [JsonPropertyName("fit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Fit { get; set; }

    [JsonPropertyName("gfpgan_strength")]
    public string GfpganStrength { get; set; }

    [JsonPropertyName("height")]
    public string Height { get; set; }

    [JsonPropertyName("initimg")]
    public string Initimg { get; set; }

    [JsonPropertyName("initimg_name")]
    public string InitimgName { get; set; } = string.Empty;

    [JsonPropertyName("invert_mask")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string InvertMask { get; set; }

    [JsonPropertyName("iterations")]
    public string Iterations { get; set; }

    [JsonPropertyName("mask")]
    public string Mask { get; set; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }

    [JsonPropertyName("sampler_name")]
    public string SamplerName { get; set; }

    [JsonPropertyName("seed")]
    public string Seed { get; set; }

    [JsonPropertyName("seamless")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Seamless { get; set; }

    [JsonPropertyName("steps")]
    public string Steps { get; set; }

    [JsonPropertyName("strength")]
    public string Strength { get; set; }

    [JsonPropertyName("upscale_level")]
    public string UpscaleLevel { get; set; }

    [JsonPropertyName("upscale_strength")]
    public string UpscaleStrength { get; set; }

    [JsonPropertyName("variation_amount")]
    public string VariationAmount { get; set; }

    [JsonPropertyName("width")]
    public string Width { get; set; }

    [JsonPropertyName("with_variations")]
    public string WithVariations { get; set; }

    public static LSteinRequest FromSettings(PromptSettings settings)
    {
        var result = new LSteinRequest
        {
            CfgScale = settings.GuidanceScale.ToString(),
            Fit = settings.Fit != Enums.OnOff.Default ? settings.Fit.ToString() : null,
            GfpganStrength = settings.EnableGfpgan ? settings.GfpganStrength.ToString() : "0",
            Height = settings.Height.ToString(),
            Initimg = settings.InitImage,
            InvertMask = settings.InvertMask != Enums.OnOff.Default ? settings.InvertMask.ToString() : null,
            Iterations = settings.BatchSize.ToString(),
            Mask = settings.Mask,
            Prompt = settings.Prompt,
            SamplerName = settings.Sampler.ToString(),
            Seamless = settings.Seamless != Enums.OnOff.Default ? settings.Seamless.ToString() : null,
            Seed = settings.Seed == -1 ? random.Next().ToString() : settings.Seed.ToString(),
            Steps = settings.Steps.ToString(),
            Strength = settings.DenoisingStrength.ToString(),
            UpscaleLevel = settings.EnableUpscaling ? settings.UpscaleLevel.ToString() : string.Empty,
            UpscaleStrength = settings.UpscaleStrength.ToString(),
            VariationAmount = settings.WithVariations == Enums.OnOff.on ? settings.VariationAmount.ToString() : "0",
            Width = settings.Width.ToString(),
            WithVariations = settings.WithVariations != Enums.OnOff.Default ? settings.WithVariations.ToString() : string.Empty,
        };

        return result;
    }
}
