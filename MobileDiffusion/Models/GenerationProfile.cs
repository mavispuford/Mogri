using MobileDiffusion.Enums;

namespace MobileDiffusion.Models;

public class GenerationProfile
{
    public int DefaultSteps { get; set; }
    public double DefaultCfg { get; set; }
    public double? DefaultDistilledCfg { get; set; }
    public required string DefaultSampler { get; set; }
    public string? DefaultScheduler { get; set; }
    public string? DefaultVae { get; set; }
    public string? DefaultTextEncoder { get; set; }
    public double DefaultWidth { get; set; }
    public double DefaultHeight { get; set; }

    public static GenerationProfile GetDefault(ModelType modelType)
    {
        return modelType switch
        {
            ModelType.ZImageTurbo => new GenerationProfile
            {
                DefaultSteps = 8,
                DefaultCfg = 1.0,
                DefaultDistilledCfg = 3.5,
                DefaultSampler = "Euler",
                DefaultScheduler = "beta",
                DefaultVae = "ae.safetensors",
                DefaultTextEncoder = "Qwen3",
                DefaultWidth = 1024,
                DefaultHeight = 1024
            },
            ModelType.Flux => new GenerationProfile
            {
                DefaultSteps = 20,
                DefaultCfg = 1.0,
                DefaultDistilledCfg = 3.5,
                DefaultSampler = "Euler",
                DefaultScheduler = "beta",
                DefaultVae = "ae.safetensors",
                DefaultTextEncoder = "t5xxl",
                DefaultWidth = 1024,
                DefaultHeight = 1024
            },
            ModelType.SD15 => new GenerationProfile
            {
                DefaultSteps = 30,
                DefaultCfg = 6.0,
                DefaultDistilledCfg = null,
                DefaultSampler = "DPM++ 2M",
                DefaultScheduler = "karras",
                DefaultWidth = 512,
                DefaultHeight = 512
            },
            _ => new GenerationProfile // SDXL
            {
                DefaultSteps = 30,
                DefaultCfg = 6.0,
                DefaultDistilledCfg = null,
                DefaultSampler = "DPM++ 2M",
                DefaultScheduler = "karras",
                DefaultWidth = 1024,
                DefaultHeight = 1024
            }
        };
    }
}
