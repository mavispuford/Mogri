using MobileDiffusion.Enums;

namespace MobileDiffusion.Models;

public class GenerationProfile
{
    public int DefaultSteps { get; set; }
    public double DefaultCfg { get; set; }
    public double? DefaultDistilledCfg { get; set; }
    public string DefaultSampler { get; set; }
    public string DefaultScheduler { get; set; }
    public double DefaultWidth { get; set; }
    public double DefaultHeight { get; set; }

    public static GenerationProfile GetDefault(ModelType modelType)
    {
        return modelType switch
        {
            ModelType.ZImage => new GenerationProfile
            {
                DefaultSteps = 8,
                DefaultCfg = 1.0,
                DefaultDistilledCfg = 9,
                DefaultSampler = "Euler",
                DefaultScheduler = "beta",
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
                DefaultWidth = 1024,
                DefaultHeight = 1024
            },
            _ => new GenerationProfile // Stable Diffusion
            {
                DefaultSteps = 30,
                DefaultCfg = 7.0,
                DefaultDistilledCfg = null,
                DefaultSampler = "DPM++ 2M",
                DefaultScheduler = null,
                DefaultWidth = 1024,
                DefaultHeight = 1024
            }
        };
    }
}
