using System.Text.RegularExpressions;
using MobileDiffusion.Models;
using MobileDiffusion.Clients.SdForgeNeo.Models;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.ViewModels;
using MobileDiffusion.Enums;

namespace MobileDiffusion.Services;

/// <summary>
/// Specialized parser for extracting generation metadata (prompts, seeds, steps) 
/// from the raw PNG text chunks created by Automatic1111/Forge WebUI.
/// </summary>
public static class ForgeMetadataParser
{
    private static class PngInfoProperties
    {
        public const string Steps = "steps";
        public const string Sampler = "sampler";
        public const string CfgScale = "cfg scale";
        public const string Seed = "seed";
        public const string Size = "size";
        public const string ModelHash = "model hash";
        public const string Model = "model";
        public const string LoraHashes = "lora hashes";
        public const string DenoisingStrength = "denoising strength";
        public const string Eta = "eta";
        public const string Version = "version";
        public const string HiresUpscaler = "hires upscaler";
        public const string HiresUpscale = "hires upscale";
        public const string HiresSteps = "hires steps";
        public const string Scheduler = "scheduler";
        public const string ScheduleType = "schedule type";
        public const string DistilledCfgScale = "distilled cfg scale";
        public const string DistilledCfgScaleKey = "distilled_cfg_scale";
        public const string Shift = "shift";
    }

    private static readonly Regex _loraRegex = new Regex("<lora:([^:]*):([^>]*)>", RegexOptions.Compiled);

    public static PromptSettings? Parse(
        string info,
        List<SDModelItem>? availableModels = null,
        Func<SDModelItem, IModelViewModel?>? modelConverter = null)
    {
        if (string.IsNullOrEmpty(info))
        {
            return new PromptSettings();
        }

        var newLineSplit = info.Split('\n');

        if (newLineSplit.Length <= 1)
        {
            return null; 
        }

        var commaSplit = newLineSplit.LastOrDefault()?.Split(',') ?? [];

        var properties = new Dictionary<string, string>();

        foreach (var item in commaSplit)
        {
            var itemSplit = item.Trim().Split(": ");

            if (itemSplit.Length == 2)
            {
                properties.TryAdd(itemSplit.First(), itemSplit.Last());
            }
        }

        var prompt = newLineSplit[0];

        var loraMatches = _loraRegex.Matches(prompt);
        var loras = new List<ILoraViewModel>();

        foreach (Match match in loraMatches)
        {
            var name = match.Groups[1].Value;

            if (loras.Any(l => l.Name == name))
            {
                continue;
            }

            if (float.TryParse(match.Groups[2].Value, out var strength))
            {
                var lora = new LoraViewModel
                {
                    Name = name,
                    Strength = strength,
                };

                loras.Add(lora);
            }

            prompt = prompt.Replace(match.Groups[0].Value, string.Empty);
        }

        var negativePrompt = newLineSplit.Length > 2 ? newLineSplit[1].Trim().Split(": ").Last() : string.Empty;

        var settings = new PromptSettings
        {
            Prompt = prompt,
            NegativePrompt = negativePrompt,
            Loras = loras
        };

        foreach (var property in properties)
        {
            try
            {
                switch (property.Key.ToLower())
                {
                    case PngInfoProperties.Steps:
                        if (int.TryParse(property.Value, out var steps)) settings.Steps = steps;
                        break;
                    case PngInfoProperties.Sampler:
                        settings.Sampler = property.Value;
                        break;
                    case PngInfoProperties.CfgScale:
                        if (double.TryParse(property.Value, out var cfg)) settings.GuidanceScale = cfg;
                        break;
                    case PngInfoProperties.Seed:
                        if (long.TryParse(property.Value, out var seed)) settings.Seed = seed;
                        break;
                    case PngInfoProperties.Size:
                        var size = property.Value.Split('x');

                        if (size.Length != 2)
                        {
                            break;
                        }

                        if (double.TryParse(size[0], out var width)) settings.Width = width;
                        if (double.TryParse(size[1], out var height)) settings.Height = height;
                        break;
                    case PngInfoProperties.DenoisingStrength:
                        if (double.TryParse(property.Value, out var denoise)) settings.DenoisingStrength = denoise;
                        break;
                    case PngInfoProperties.HiresUpscaler:
                        settings.Upscaler = property.Value;
                        settings.EnableUpscaling = !string.IsNullOrEmpty(property.Value);
                        break;
                    case PngInfoProperties.HiresUpscale:
                        if (int.TryParse(property.Value, out var upscale)) settings.UpscaleLevel = upscale;
                        break;
                    case PngInfoProperties.HiresSteps:
                        if (int.TryParse(property.Value, out var hrSteps)) settings.UpscaleSteps = hrSteps;
                        break;
                    case PngInfoProperties.Scheduler:
                    case PngInfoProperties.ScheduleType:
                        settings.Scheduler = property.Value.ToLower();
                        break;
                    case PngInfoProperties.DistilledCfgScale:
                    case PngInfoProperties.DistilledCfgScaleKey:
                    case PngInfoProperties.Shift:
                        if (double.TryParse(property.Value, out var distCfg)) settings.DistilledCfgScale = distCfg;
                        break;
                    case PngInfoProperties.Model:
                        if (settings.Model == null && availableModels != null && modelConverter != null)
                        {
                            var matchingModel = availableModels.FirstOrDefault(m => m.ModelName == property.Value);
                            if (matchingModel != null)
                            {
                                settings.Model = modelConverter(matchingModel);
                            }
                        }
                        break;
                    case PngInfoProperties.ModelHash:
                        if (settings.Model == null && availableModels != null && modelConverter != null)
                        {
                            var matchingModel = availableModels.FirstOrDefault(m => m.Hash == property.Value);
                            if (matchingModel != null)
                            {
                                settings.Model = modelConverter(matchingModel);
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            catch
            {
                // Skip to the next property
            }
        }

        return settings;
    }
}
