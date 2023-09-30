using MobileDiffusion.Enums;
using System.Text.Json.Serialization;

namespace MobileDiffusion.Models.LStein;

public class LSteinResponseItem
{
    [JsonPropertyName("event")]
    public string Event { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("seed")]
    public long Seed { get; set; }

    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("config")]
    public LSteinConfig Config { get; set; }

    public static PromptSettings ToSettings(LSteinResponseItem responseItem)
    {
        if (responseItem == null)
        {
            return null;
        }

        var config = responseItem.Config;
        var defaultSettings = new PromptSettings();

        var result = new PromptSettings
        {
            InitImage = string.IsNullOrEmpty(config.Initimg) ? null : config.Initimg,
            Mask = string.IsNullOrEmpty(config.Mask) ? null : config.Mask,
            Prompt = config.Prompt,
            Seed = responseItem.Seed,
        };

        if (double.TryParse(config.CfgScale, out var guidanceScale))
        {
            result.GuidanceScale = guidanceScale;
        }

        if (Enum.TryParse<OnOff>(config.Fit, out var fit))
        {
            result.Fit = fit;
        }

        if (double.TryParse(config.GfpganStrength, out var gfpganStrength))
        {
            result.GfpganStrength = gfpganStrength;
            result.EnableGfpgan = gfpganStrength != 0;

            if (!result.EnableGfpgan)
            {
                result.GfpganStrength = defaultSettings.GfpganStrength;
            }
        }

        if (double.TryParse(config.Height, out var height))
        {
            result.Height = height;
        }

        if (Enum.TryParse<OnOff>(config.InvertMask, out var invertMask))
        {
            result.InvertMask = invertMask;
        }

        if (int.TryParse(config.Iterations, out var numOutputs))
        {
            result.BatchSize = numOutputs;
        }

        result.Sampler = config.SamplerName;

        if (Enum.TryParse<OnOff>(config.Seamless, out var seamless))
        {
            result.Seamless = seamless;
        }

        if (int.TryParse(config.Steps, out var numInferenceSteps))
        {
            result.Steps = numInferenceSteps;
        }

        if (double.TryParse(config.Strength, out var strength))
        {
            result.DenoisingStrength = strength;
        }

        if (int.TryParse(config.UpscaleLevel, out var upscaleLevel))
        {
            result.UpscaleLevel = upscaleLevel;
            result.EnableUpscaling = upscaleLevel != 0;

            if (!result.EnableUpscaling)
            {
                result.UpscaleLevel = defaultSettings.UpscaleLevel;
            }
        }

        if (double.TryParse(config.UpscaleStrength, out var upscaleStrength))
        {
            result.UpscaleSteps = (int)(upscaleStrength * numInferenceSteps);

            if (!result.EnableUpscaling)
            {
                result.UpscaleSteps = defaultSettings.UpscaleSteps;
            }
        }

        if (double.TryParse(config.VariationAmount, out var variationAmount))
        {
            result.VariationAmount = variationAmount;
        }

        if (double.TryParse(config.Width, out var width))
        {
            result.Width = width;
        }

        if (Enum.TryParse<OnOff>(config.WithVariations, out var withVariations))
        {
            result.WithVariations = withVariations;
        }

        return result;
    }

}
