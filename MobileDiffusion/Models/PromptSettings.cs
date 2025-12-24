using MobileDiffusion.Enums;
using MobileDiffusion.ViewModels;
using Newtonsoft.Json;

namespace MobileDiffusion.Models;

public class PromptSettings
{
    public bool EnableGfpgan { get; set; } = false;
    public bool EnableUpscaling { get; set; } = false;
    public OnOff Fit { get; set; } = OnOff.on;
    public bool FitClientSide { get; set; } = true;
    public double GfpganStrength { get; set; } = .75;
    public double GuidanceScale { get; set; } = 7.5;
    public double Height { get; set; } = Preferences.Default.Get<double>(Constants.PreferenceKeys.DefaultWidth, 512);
    public string InitImage { get; set; }
    public OnOff InvertMask { get; set; }
    public string Mask { get; set; }
    public int Steps { get; set; } = 30;
    public int BatchCount { get; set; } = 1;
    public int BatchSize { get; set; } = 1;
    public string Prompt { get; set; }
    public string NegativePrompt { get; set; }
    public double DenoisingStrength { get; set; } = .5;
    public ModelViewModel Model { get; set; }
    public string Sampler { get; set; }
    public OnOff Seamless { get; set; }
    public long Seed { get; set; } = -1;
    public string Upscaler { get; set; }
    public int UpscaleLevel { get; set; } = 2;
    public int UpscaleSteps { get; set; } = 10;
    public double VariationAmount { get; set; } = .1;
    public double Width { get; set; } = Preferences.Default.Get<double>(Constants.PreferenceKeys.DefaultHeight, 512);
    public OnOff WithVariations { get; set; }
    public List<LoraViewModel> Loras { get; set; } = new();
    public List<PromptStyleViewModel> PromptStyles { get; set; } = new();
    
    public PromptSettings Clone()
    {
        var json = JsonConvert.SerializeObject(this);

        return JsonConvert.DeserializeObject<PromptSettings>(json);
    }
}
