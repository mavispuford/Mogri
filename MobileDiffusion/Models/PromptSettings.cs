using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Models;

public class PromptSettings
{
    public bool EnableGfpgan { get; set; } = false;
    public bool EnableUpscaling { get; set; } = false;
    public OnOff Fit { get; set; } = OnOff.on;
    public bool FitClientSide { get; set; } = true;
    public double GfpganStrength { get; set; } = .75;
    public double GuidanceScale { get; set; } = 7.5;
    public double? DistilledCfgScale { get; set; } = 3;
    public double Height { get; set; } = Preferences.Default.Get<double>(Constants.PreferenceKeys.DefaultWidth, 512);
    public string? InitImage { get; set; }
    public string? InitImageThumbnail { get; set; }
    public OnOff InvertMask { get; set; }
    public string? Mask { get; set; }
    public int MaskBlur { get; set; } = 0;
    public ModelType ModelType { get; set; } = ModelType.StableDiffusion;
    public int Steps { get; set; } = 30;
    public int BatchCount { get; set; } = 1;
    public int BatchSize { get; set; } = 1;
    public string Prompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public double DenoisingStrength { get; set; } = .5;
    public IModelViewModel? Model { get; set; }
    public string? Sampler { get; set; }
    public string? Scheduler { get; set; }
    public OnOff Seamless { get; set; }
    public long Seed { get; set; } = -1;
    public string? Upscaler { get; set; }
    public int UpscaleLevel { get; set; } = 2;
    public int UpscaleSteps { get; set; } = 10;
    public double VariationAmount { get; set; } = .1;
    public double Width { get; set; } = Preferences.Default.Get<double>(Constants.PreferenceKeys.DefaultHeight, 512);
    public OnOff WithVariations { get; set; }
    public List<ILoraViewModel> Loras { get; set; } = new();
    public List<IPromptStyleViewModel> PromptStyles { get; set; } = new();

    public PromptSettings Clone()
    {
        return new PromptSettings
        {
            EnableGfpgan = EnableGfpgan,
            EnableUpscaling = EnableUpscaling,
            Fit = Fit,
            FitClientSide = FitClientSide,
            GfpganStrength = GfpganStrength,
            GuidanceScale = GuidanceScale,
            DistilledCfgScale = DistilledCfgScale,
            Height = Height,
            InitImage = InitImage,
            InitImageThumbnail = InitImageThumbnail,
            InvertMask = InvertMask,
            Mask = Mask,
            MaskBlur = MaskBlur,
            ModelType = ModelType,
            Steps = Steps,
            BatchCount = BatchCount,
            BatchSize = BatchSize,
            Prompt = Prompt,
            NegativePrompt = NegativePrompt,
            DenoisingStrength = DenoisingStrength,
            Model = Model,
            Sampler = Sampler,
            Scheduler = Scheduler,
            Seamless = Seamless,
            Seed = Seed,
            Upscaler = Upscaler,
            UpscaleLevel = UpscaleLevel,
            UpscaleSteps = UpscaleSteps,
            VariationAmount = VariationAmount,
            Width = Width,
            WithVariations = WithVariations,
            Loras = new List<ILoraViewModel>(Loras),
            PromptStyles = new List<IPromptStyleViewModel>(PromptStyles),
        };
    }
}
