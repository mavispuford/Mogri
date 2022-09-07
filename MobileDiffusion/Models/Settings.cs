using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Models
{
    public class Settings
    {
        public double GuidanceScale { get; set; } = 7.5;
        public Fit Fit { get; set; } = Fit.on;
        public double Height { get; set; } = 512;
        public double PromptStrength { get; set; } = .75;
        public double Width { get; set; } = 512;
        public int NumInferenceSteps { get; set; } = 50;
        public int NumOutputs { get; set; } = 1;
        public Sampler Sampler { get; set; } = Sampler.k_lms;
        public long Seed { get; set; } = -1;
        public string InitImage { get; set; }
        public string Prompt { get; set; }

        public Settings Clone()
        {
            var clone = new Settings
            {
                GuidanceScale = GuidanceScale,
                Fit = Fit,
                Height = Height,
                PromptStrength = PromptStrength,
                Width = Width,
                NumInferenceSteps = NumInferenceSteps,
                NumOutputs = NumOutputs,
                Sampler = Sampler,
                Seed = Seed,
                InitImage = InitImage,
                Prompt = Prompt,
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

            var result = new Settings
            {
                InitImage = string.IsNullOrEmpty(config.Initimg) ? null : config.Initimg,
                Prompt = config.Prompt,
                Seed = responseItem.Seed,
            };

            if (double.TryParse(config.Cfgscale, out var guidanceScale))
            {
                result.GuidanceScale = guidanceScale;
            }

            if (Enum.TryParse<Fit>(config.Fit, out var fit))
            {
                result.Fit = fit;
            }

            if (double.TryParse(config.Height, out var height))
            {
                result.Height = height;
            }

            if (int.TryParse(config.Iterations, out var numOutputs))
            {
                result.NumOutputs = numOutputs;
            }

            if (Enum.TryParse<Sampler>(config.Sampler, out var sampler))
            {
                result.Sampler = sampler;
            }

            if (int.TryParse(config.Steps, out var numInferenceSteps))
            {
                result.NumInferenceSteps = numInferenceSteps;
            }

            if (double.TryParse(config.Strength, out var strength))
            {
                result.PromptStrength = strength;
            }

            if (double.TryParse(config.Width, out var width))
            {
                result.Width = width;
            }

            return result;
        }
    }
}
