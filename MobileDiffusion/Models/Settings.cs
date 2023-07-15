using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;
using Newtonsoft.Json;

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
    public int Steps { get; set; } = 50;
    public int NumOutputs { get; set; } = 1;
    public string Prompt { get; set; }
    public string NegativePrompt { get; set; }
    public double DenoisingStrength { get; set; } = .75;
    public string Sampler { get; set; }
    public OnOff Seamless { get; set; }
    public long Seed { get; set; } = -1;
    public int UpscaleLevel { get; set; } = 2;
    public double UpscaleStrength { get; set; } = .75;
    public double VariationAmount { get; set; } = .1;
    public double Width { get; set; } = 512;
    public OnOff WithVariations { get; set; }
    public List<PromptStyleViewModel> PromptStyles { get; set; } = new();

    public Settings Clone()
    {
        var json = JsonConvert.SerializeObject(this);

        return JsonConvert.DeserializeObject<Settings>(json);
    }
}
