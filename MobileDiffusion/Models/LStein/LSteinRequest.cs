using System.Text.Json.Serialization;

namespace MobileDiffusion.Models.LStein;

public class LSteinRequest
{
    private static Random random = new Random();
    
    [JsonPropertyName("cfg_scale")]
    public string CfgScale { get; set; }
    
    [JsonPropertyName("fit")]
    public string Fit { get; set; }

    [JsonPropertyName("gfpgan_strength")]
    public string GfpganStrength { get; set; }

    [JsonPropertyName("height")]
    public string Height { get; set; }

    [JsonPropertyName("initimg")]
    public string Initimg { get; set; }

    [JsonPropertyName("initimg_name")]
    public string InitimgName { get; set; } = string.Empty;

    [JsonPropertyName("iterations")]
    public string Iterations { get; set; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }

    [JsonPropertyName("sampler_name")]
    public string SamplerName { get; set; }

    [JsonPropertyName("seed")]
    public string Seed { get; set; }
    
    [JsonPropertyName("steps")]
    public string Steps { get; set; }

    [JsonPropertyName("strength")]
    public string Strength { get; set; }

    [JsonPropertyName("upscale_level")]
    public string UpscaleLevel { get; set; }

    [JsonPropertyName("upscale_strength")]
    public string UpscaleStrength { get; set; }

    [JsonPropertyName("variation_amount")]
    public string VariationAmount { get; set; } = "0";

    [JsonPropertyName("width")]
    public string Width { get; set; }

    [JsonPropertyName("with_variations")]
    public string WithVariations { get; set; } = string.Empty;

    public static LSteinRequest FromSettings(Settings settings)
    {
        var result = new LSteinRequest
        {
            CfgScale = settings.GuidanceScale.ToString(),
            Fit = settings.Fit.ToString(),
            Height = settings.Height.ToString(),
            Initimg = settings.InitImage,
            Iterations = settings.NumOutputs.ToString(),
            Prompt = settings.Prompt,
            SamplerName = settings.Sampler.ToString(),
            Seed = settings.Seed == -1 ? random.Next().ToString() : settings.Seed.ToString(),
            Steps = settings.NumInferenceSteps.ToString(),
            Strength = settings.PromptStrength.ToString(),
            Width = settings.Width.ToString(),
            UpscaleLevel = settings.UpscaleLevel.ToString(),
            UpscaleStrength = settings.UpscaleStrength.ToString(),
        };

        if (settings.EnableGfpgan)
        {
            result.GfpganStrength = settings.GfpganStrength.ToString();
        }
        else
        {
            result.GfpganStrength = "0";
        }

        if (settings.EnableUpscaling)
        {
            result.UpscaleLevel = settings.UpscaleLevel.ToString();
        }
        else 
        {
            result.UpscaleLevel = string.Empty;
        }

        return result;
    }
}
