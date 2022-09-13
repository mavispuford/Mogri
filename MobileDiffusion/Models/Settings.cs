using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Models
{
    public class Settings
    {
        public bool EnableGfpgan { get; set; } = false;
        public bool EnableUpscaling { get; set; } = false;
        public OnOff Fit { get; set; } = OnOff.on;
        public double GfpganStrength { get; set; } = .75;
        public double GuidanceScale { get; set; } = 7.5;
        public double Height { get; set; } = 512;
        public string InitImage { get; set; }
        public OnOff InvertMask { get; set; }
        public string Mask { get; set; }
        public int NumInferenceSteps { get; set; } = 50;
        public int NumOutputs { get; set; } = 1;
        public string Prompt { get; set; }
        public double PromptStrength { get; set; } = .75;
        public Sampler Sampler { get; set; } = Sampler.k_lms;
        public OnOff Seamless { get; set; }
        public long Seed { get; set; } = -1;
        public int UpscaleLevel { get; set; } = 2;
        public double UpscaleStrength { get; set; } = .75;
        public double VariationAmount { get; set; } = .1;
        public double Width { get; set; } = 512;
        public OnOff WithVariations { get; set; }

        public Settings Clone()
        {
            var clone = new Settings
            {
                EnableGfpgan = EnableGfpgan,
                EnableUpscaling = EnableUpscaling,
                Fit = Fit,
                GfpganStrength = GfpganStrength,
                GuidanceScale = GuidanceScale,
                Height = Height,
                InitImage = InitImage,
                Mask = Mask,
                NumInferenceSteps = NumInferenceSteps,
                NumOutputs = NumOutputs,
                Prompt = Prompt,
                PromptStrength = PromptStrength,
                Sampler = Sampler,
                Seamless = Seamless,
                Seed = Seed,
                UpscaleLevel = UpscaleLevel,
                UpscaleStrength = UpscaleStrength,
                VariationAmount = VariationAmount,
                Width = Width,
                WithVariations = WithVariations,
            };

            return clone;
        }

        public static Settings FromResultItem(IResultItemViewModel resultItem)
        {
            if (resultItem?.ResponseItem == null)
            {
                return null;
            }

            var responseItem = resultItem.ResponseItem;
            var config = responseItem.Config;
            var defaultSettings = new Settings();

            var result = new Settings
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
                result.NumOutputs = numOutputs;
            }

            if (Enum.TryParse<Sampler>(config.SamplerName, out var sampler))
            {
                result.Sampler = sampler;
            }

            if (Enum.TryParse<OnOff>(config.Seamless, out var seamless))
            {
                result.Seamless = seamless;
            }

            if (int.TryParse(config.Steps, out var numInferenceSteps))
            {
                result.NumInferenceSteps = numInferenceSteps;
            }

            if (double.TryParse(config.Strength, out var strength))
            {
                result.PromptStrength = strength;
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
                result.UpscaleStrength = upscaleStrength;

                if (!result.EnableUpscaling)
                {
                    result.UpscaleStrength = defaultSettings.UpscaleStrength;
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
}
