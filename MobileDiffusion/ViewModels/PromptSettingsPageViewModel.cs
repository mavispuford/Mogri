using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.ViewModels;

public partial class PromptSettingsPageViewModel : PageViewModel, IPromptSettingsPageViewModel
{
    private Settings _settings;

    [ObservableProperty]
    private List<string> availableWidthValues = new();

    [ObservableProperty]
    private List<string> availableHeightValues = new();

    [ObservableProperty]
    private List<string> availableSamplerValues = new();

    [ObservableProperty]
    private List<string> availableUpscaleLevelValues = new();

    [ObservableProperty]
    private string imageCount;

    [ObservableProperty]
    private string imageCountPlaceholder;

    [ObservableProperty]
    private string steps;

    [ObservableProperty]
    private string stepsPlaceholder;

    [ObservableProperty]
    private string cfgScale;

    [ObservableProperty]
    private string cfgScalePlaceholder;
    
    [ObservableProperty]
    private string sampler;

    [ObservableProperty]
    private string width;

    [ObservableProperty]
    private string height;

    [ObservableProperty]
    private string seed;

    [ObservableProperty]
    private string seedPlaceholder;

    [ObservableProperty]
    private bool enableGfpgan;

    [ObservableProperty]
    private string gfpganStrength;

    [ObservableProperty]
    private string gfpganStrengthPlaceholder;

    [ObservableProperty]
    private bool enableUpscaling;

    [ObservableProperty]
    private string upscaleLevel;

    [ObservableProperty]
    private string upscaleStrength;

    [ObservableProperty]
    private string upscaleStrengthPlaceholder;

    public PromptSettingsPageViewModel()
    {
        var widthValues = new List<string>();
        var heightValues = new List<string>();

        for (var i = 64; i <= 2048; i += 64)
        {
            widthValues.Add(i.ToString());
            heightValues.Add(i.ToString());
        }

        var samplerValues = new List<string>();

        foreach (var value in Enum.GetNames(typeof(Sampler)))
        {
            samplerValues.Add(value);
        }

        var upscaleLevelValues = new List<string>
        {
            "2","4"
        };

        availableWidthValues = widthValues;
        availableHeightValues = heightValues;
        availableSamplerValues = samplerValues;
        availableUpscaleLevelValues = upscaleLevelValues;

        var defaultSettings = new Settings();
        ImageCountPlaceholder = defaultSettings.NumOutputs.ToString();
        StepsPlaceholder = defaultSettings.NumInferenceSteps.ToString();
        CfgScalePlaceholder = defaultSettings.GuidanceScale.ToString();
        SeedPlaceholder = defaultSettings.Seed.ToString();
        GfpganStrengthPlaceholder = defaultSettings.GfpganStrength.ToString();
        UpscaleStrengthPlaceholder = defaultSettings.UpscaleStrength.ToString();
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) ||
            promptSettings is not Settings settings)
        {
            throw new ArgumentException(nameof(NavigationParams.PromptSettings));
        }

        _settings = settings.Clone();

        mapSettingsToProperties();
    }

    [RelayCommand]
    private async Task ResetValues()
    {
        var result = await Shell.Current.DisplayAlert("Confirm Reset", "Are you sure you would like to reset back to defaults?", "RESET", "Cancel");
        
        if (!result)
        {
            return;
        }

        var defaultSettings = new Settings();
        ImageCount = defaultSettings.NumOutputs.ToString();
        Steps = defaultSettings.NumInferenceSteps.ToString();
        CfgScale = defaultSettings.GuidanceScale.ToString();
        Sampler = defaultSettings.Sampler.ToString();
        Width = defaultSettings.Width.ToString();
        Height = defaultSettings.Height.ToString();
        Seed = defaultSettings.Seed.ToString();
        EnableGfpgan = defaultSettings.EnableGfpgan;
        GfpganStrength = defaultSettings.GfpganStrength.ToString();
        EnableUpscaling = defaultSettings.EnableUpscaling;
        UpscaleLevel = defaultSettings.UpscaleLevel.ToString();
        UpscaleStrength = defaultSettings.UpscaleStrength.ToString();
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task ConfirmSettings()
    {
        mapPropertiesToSettings();

        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

        await Shell.Current.GoToAsync("..", parameters);
    }

    private void mapSettingsToProperties()
    {
        ImageCount = _settings.NumOutputs.ToString();
        Steps = _settings.NumInferenceSteps.ToString();
        CfgScale = _settings.GuidanceScale.ToString();
        Sampler = _settings.Sampler.ToString();
        Width = _settings.Width.ToString();
        Height = _settings.Height.ToString();
        Seed = _settings.Seed.ToString();
        EnableGfpgan = _settings.EnableGfpgan;
        GfpganStrength = _settings.GfpganStrength.ToString();
        EnableUpscaling = _settings.EnableUpscaling;
        UpscaleLevel = _settings.UpscaleLevel == 0 ? "2" : _settings.UpscaleLevel.ToString();
        UpscaleStrength = _settings.UpscaleStrength.ToString();
    }

    private void mapPropertiesToSettings()
    {
        if (int.TryParse(ImageCount, out var pImageCount) ||
            int.TryParse(ImageCountPlaceholder, out pImageCount))
        {
            _settings.NumOutputs = pImageCount;
        }

        if (int.TryParse(Steps, out var pSteps) ||
            int.TryParse(StepsPlaceholder, out pSteps))
        {
            _settings.NumInferenceSteps = pSteps;
        }

        if (double.TryParse(CfgScale, out var pCfgScale) ||
            double.TryParse(CfgScalePlaceholder, out pCfgScale))
        {
            _settings.GuidanceScale = pCfgScale;
        }

        if (Enum.TryParse<Sampler>(Sampler, out var pSampler))
        {
            _settings.Sampler = pSampler;
        }

        if (double.TryParse(Width, out var pWidth))
        {
            _settings.Width = pWidth;
        }

        if (double.TryParse(Height, out var pHeight))
        {
            _settings.Height = pHeight;
        }

        if (long.TryParse(Seed, out var pSeed) ||
            long.TryParse(SeedPlaceholder, out pSeed))
        {
            _settings.Seed = pSeed;
        }

        _settings.EnableGfpgan = EnableGfpgan;

        if (double.TryParse(GfpganStrength, out var pGfpganStrength) ||
            double.TryParse(GfpganStrengthPlaceholder, out pGfpganStrength))
        {
            _settings.GfpganStrength = pGfpganStrength;
        }

        _settings.EnableUpscaling = EnableUpscaling;

        if (int.TryParse(UpscaleLevel, out var pUpscaleLevel))
        {
            _settings.UpscaleLevel = pUpscaleLevel;
        }

        if (double.TryParse(UpscaleStrength, out var pUpscaleStrength))
        {
            _settings.UpscaleStrength = pUpscaleStrength;
        }
    }
}
