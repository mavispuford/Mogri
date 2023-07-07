using MobileDiffusion.Enums;

namespace MobileDiffusion.Models;

public class Settings
{
    public bool EnableGfpgan { get; set; } = false;
    public bool EnableUpscaling { get; set; } = false;
    public OnOff Fit { get; set; } = OnOff.on;
    public bool FitClientSide { get; set; } = true;
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
            FitClientSide = FitClientSide,
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
}
