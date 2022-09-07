using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.ViewModels;

public partial class PromptSettingsPopupViewModel : PopupBaseViewModel, IPromptSettingsPopupViewModel
{
    private Settings _settings;

    [ObservableProperty]
    private List<string> availableWidthValues = new();

    [ObservableProperty]
    private List<string> availableHeightValues = new();

    [ObservableProperty]
    private List<string> availableSamplerValues = new();

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

    public PromptSettingsPopupViewModel(IPopupService popupService) : base(popupService)
    {
        var widthValues = new List<string>();
        var heightValues = new List<string>();


        for (var i = 64; i <= 1024; i += 64)
        {
            widthValues.Add(i.ToString());
            heightValues.Add(i.ToString());
        }

        var samplerValues = new List<string>();

        foreach (var value in Enum.GetNames(typeof(Sampler)))
        {
            samplerValues.Add(value);
        }

        availableWidthValues = widthValues;
        availableHeightValues = heightValues;
        availableSamplerValues = samplerValues;

        var defaultSettings = new Settings();
        ImageCountPlaceholder = defaultSettings.NumOutputs.ToString();
        StepsPlaceholder = defaultSettings.NumInferenceSteps.ToString();
        CfgScalePlaceholder = defaultSettings.GuidanceScale.ToString();
        SeedPlaceholder = defaultSettings.Seed.ToString();
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
    private void ResetValues()
    {
        var defaultSettings = new Settings();
        ImageCount = defaultSettings.NumOutputs.ToString();
        Steps = defaultSettings.NumInferenceSteps.ToString();
        CfgScale = defaultSettings.GuidanceScale.ToString();
        Sampler = defaultSettings.Sampler.ToString();
        Width = defaultSettings.Width.ToString();
        Height = defaultSettings.Height.ToString();
        Seed = defaultSettings.Seed.ToString();
    }

    [RelayCommand]
    private void Cancel()
    {
        ClosePopup(null);
    }

    [RelayCommand]
    private void ConfirmSettings()
    {
        mapPropertiesToSettings();

        ClosePopup(_settings);
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
    }

    private void mapPropertiesToSettings()
    {
        if (int.TryParse(ImageCount, out var imageCount) ||
            int.TryParse(ImageCountPlaceholder, out imageCount))
        {
            _settings.NumOutputs = imageCount;
        }

        if (int.TryParse(Steps, out var steps) ||
            int.TryParse(StepsPlaceholder, out steps))
        {
            _settings.NumInferenceSteps = steps;
        }

        if (double.TryParse(CfgScale, out var cfgScale) ||
            double.TryParse(CfgScalePlaceholder, out cfgScale))
        {
            _settings.GuidanceScale = cfgScale;
        }

        if (Enum.TryParse<Sampler>(Sampler, out var sampler))
        {
            _settings.Sampler = sampler;
        }

        if (double.TryParse(Width, out var width))
        {
            _settings.Width = width;
        }

        if (double.TryParse(Height, out var height))
        {
            _settings.Height = height;
        }

        if (long.TryParse(Seed, out var seed) ||
            long.TryParse(SeedPlaceholder, out seed))
        {
            _settings.Seed = seed;
        }
    }
}
