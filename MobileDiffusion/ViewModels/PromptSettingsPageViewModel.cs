using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.ViewModels;

public partial class PromptSettingsPageViewModel : PageViewModel, IPromptSettingsPageViewModel
{
    private readonly IStableDiffusionService _stableDiffusionService;
    private readonly IPopupService _popupService;

    private Settings _settings;

    [ObservableProperty]
    private List<string> availableSamplerValues = new();

    [ObservableProperty]
    private List<string> availableUpscaleLevelValues = new();

    [ObservableProperty]
    private string batchCount;

    [ObservableProperty]
    private string batchSize;

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

    [ObservableProperty]
    private bool makeSeamless;

    public PromptSettingsPageViewModel(IStableDiffusionService stableDiffusionService,
        IPopupService popupService)
    {
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));

        var upscaleLevelValues = new List<string>
        {
            "2","4"
        };

        AvailableUpscaleLevelValues = upscaleLevelValues;

        var defaultSettings = new Settings();
        StepsPlaceholder = defaultSettings.Steps.ToString();
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

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        try
        {
            var samplers = await _stableDiffusionService.GetSamplersAsync();

            AvailableSamplerValues = samplers.Select(s => s.Key).ToList();
        }
        catch
        {
            // TODO - Handle this
        }

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
        CfgScale = defaultSettings.GuidanceScale.ToString();
        EnableGfpgan = defaultSettings.EnableGfpgan;
        EnableUpscaling = defaultSettings.EnableUpscaling;
        GfpganStrength = defaultSettings.GfpganStrength.ToString();
        Height = defaultSettings.Height.ToString();
        BatchCount = defaultSettings.BatchCount.ToString();
        BatchSize = defaultSettings.BatchSize.ToString();
        MakeSeamless = defaultSettings.Seamless == OnOff.on;
        Sampler = defaultSettings.Sampler;
        Seed = defaultSettings.Seed.ToString();
        Steps = defaultSettings.Steps.ToString();
        UpscaleLevel = defaultSettings.UpscaleLevel.ToString();
        UpscaleStrength = defaultSettings.UpscaleStrength.ToString();
        Width = defaultSettings.Width.ToString();
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

    [RelayCommand]
    private async Task ShowResolutionSelect()
    {
        var parameters = new Dictionary<string, object> {
            { NavigationParams.Width, Width },
            { NavigationParams.Height, Height },
        };

        var result = await _popupService.ShowPopupAsync("ResolutionSelectPopup", parameters) as IDictionary<string, object>;

        if (result != null)
        {
            if (result.TryGetValue(NavigationParams.Width, out var widthParam) &&
                widthParam is double width)
            {
                Width = width.ToString();
            }

            if (result.TryGetValue(NavigationParams.Height, out var heightParam) &&
                heightParam is double height)
            {
                Height = height.ToString();
            }
        }
    }

    public override bool OnBackButtonPressed()
    {
        ConfirmSettingsCommand.Execute(null);

        return true;
    }

    private void mapSettingsToProperties()
    {
        CfgScale = _settings.GuidanceScale.ToString();
        EnableGfpgan = _settings.EnableGfpgan;
        EnableUpscaling = _settings.EnableUpscaling;
        GfpganStrength = _settings.GfpganStrength.ToString();
        Height = _settings.Height.ToString();
        BatchCount = _settings.BatchCount.ToString();
        BatchSize = _settings.BatchSize.ToString();
        MakeSeamless = _settings.Seamless == OnOff.on;
        Sampler = _settings.Sampler;
        Seed = _settings.Seed.ToString();
        Steps = _settings.Steps.ToString();
        UpscaleLevel = _settings.UpscaleLevel == 0 ? "2" : _settings.UpscaleLevel.ToString();
        UpscaleStrength = _settings.UpscaleStrength.ToString();
        Width = _settings.Width.ToString();
    }

    private void mapPropertiesToSettings()
    {
        if (double.TryParse(CfgScale, out var pCfgScale) ||
            double.TryParse(CfgScalePlaceholder, out pCfgScale))
        {
            _settings.GuidanceScale = pCfgScale;
        }

        _settings.EnableGfpgan = EnableGfpgan;
        _settings.EnableUpscaling = EnableUpscaling;

        if (double.TryParse(GfpganStrength, out var pGfpganStrength) ||
            double.TryParse(GfpganStrengthPlaceholder, out pGfpganStrength))
        {
            _settings.GfpganStrength = pGfpganStrength;
        }

        if (double.TryParse(Height, out var pHeight))
        {
            _settings.Height = pHeight;
        }

        if (int.TryParse(BatchCount, out var pBatchCount))
        {
            _settings.BatchCount = pBatchCount;
        }

        if (int.TryParse(BatchSize, out var pBatchSize))
        {
            _settings.BatchSize = pBatchSize;
        }

        _settings.Seamless = MakeSeamless ? OnOff.on : OnOff.Default;

        _settings.Sampler = Sampler;

        if (long.TryParse(Seed, out var pSeed) ||
            long.TryParse(SeedPlaceholder, out pSeed))
        {
            _settings.Seed = pSeed;
        }

        if (int.TryParse(Steps, out var pSteps) ||
            int.TryParse(StepsPlaceholder, out pSteps))
        {
            _settings.Steps = pSteps;
        }

        if (int.TryParse(UpscaleLevel, out var pUpscaleLevel))
        {
            _settings.UpscaleLevel = pUpscaleLevel;
        }

        if (double.TryParse(UpscaleStrength, out var pUpscaleStrength))
        {
            _settings.UpscaleStrength = pUpscaleStrength;
        }

        if (double.TryParse(Width, out var pWidth))
        {
            _settings.Width = pWidth;
        }
    }
}
