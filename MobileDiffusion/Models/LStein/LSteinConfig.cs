using System.Text.Json.Serialization;

namespace MobileDiffusion.Models.LStein;

public class LSteinConfig
{
    [JsonPropertyName("cfg_scale")]
    public string CfgScale { get; set; }

    [JsonPropertyName("iterations")]
    public string Iterations { get; set; }

    [JsonPropertyName("fit")]
    public string Fit { get; set; }
    
    [JsonPropertyName("gfpgan_strength")]
    public string GfpganStrength { get; set; }

    [JsonPropertyName("height")]
    public string Height { get; set; }

    [JsonPropertyName("initimg")]
    public string Initimg { get; set; }

    [JsonPropertyName("invert_mask")]
    public string InvertMask { get; set; }

    [JsonPropertyName("mask")]
    public string Mask { get; set; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }

    [JsonPropertyName("sampler_name")]
    public string SamplerName { get; set; }    
    
    [JsonPropertyName("seed")]
    public string Seed { get; set; }

    [JsonPropertyName("seamless")]
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
}
