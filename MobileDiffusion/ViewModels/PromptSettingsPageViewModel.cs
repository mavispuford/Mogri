using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.ViewModels;

public partial class PromptSettingsPageViewModel : BaseViewModel, IPromptSettingsPageViewModel, IQueryAttributable
{
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

    public PromptSettingsPageViewModel()
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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) ||
            promptSettings is not Settings settings)
        {
            throw new ArgumentException(nameof(NavigationParams.PromptSettings));
        }

        mapSettingsToProperties(settings);
    }

    [RelayCommand]
    private void ResetValues()
    {
        mapSettingsToProperties(new Settings());
    }

    [RelayCommand]
    private void ConfirmSettings()
    {
        var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, mapPropertiesToSettings() } };

        Shell.Current.GoToAsync("..", parameters);
    }

    private void mapSettingsToProperties(Settings settings)
    {
        ImageCount = settings.NumOutputs.ToString();
        Steps = settings.NumInferenceSteps.ToString();
        CfgScale = settings.GuidanceScale.ToString();
        Sampler = settings.Sampler.ToString();
        Width = settings.Width.ToString();
        Height = settings.Height.ToString();
        Seed = settings.Seed.ToString();
    }

    private Settings mapPropertiesToSettings()
    {
        var settings = new Settings();

        if (int.TryParse(ImageCount, out var imageCount) ||
            int.TryParse(ImageCountPlaceholder, out imageCount))
        {
            settings.NumOutputs = imageCount;
        }

        if (int.TryParse(Steps, out var steps) ||
            int.TryParse(StepsPlaceholder, out steps))
        {
            settings.NumInferenceSteps = steps;
        }

        if (double.TryParse(CfgScale, out var cfgScale) ||
            double.TryParse(CfgScalePlaceholder, out cfgScale))
        {
            settings.GuidanceScale = cfgScale;
        }

        if (Enum.TryParse<Sampler>(Sampler, out var sampler))
        {
            settings.Sampler = sampler;
        }

        if (double.TryParse(Width, out var width))
        {
            settings.Width = width;
        }

        if (double.TryParse(Height, out var height))
        {
            settings.Height = height;
        }

        if (long.TryParse(Seed, out var seed) ||
            long.TryParse(SeedPlaceholder, out seed))
        {
            settings.Seed = seed;
        }

        return settings;
    }
}
