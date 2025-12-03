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

    private PromptSettings _settings;
    
    [ObservableProperty]
    private List<IModelViewModel> _availableModelValues = new();

    [ObservableProperty]
    private List<string> _availableSamplerValues = new();

    [ObservableProperty]
    private List<string> _availableUpscaleLevelValues = new();

    [ObservableProperty]
    private List<string> _availableUpscalerValues = new();

    [ObservableProperty]
    private string _batchCount;

    [ObservableProperty]
    private string _batchSize;

    [ObservableProperty]
    private string _steps;

    [ObservableProperty]
    private string _stepsPlaceholder;

    [ObservableProperty]
    private string _cfgScale;

    [ObservableProperty]
    private string _cfgScalePlaceholder;

    [ObservableProperty]
    private IModelViewModel _model;

    [ObservableProperty]
    private string _sampler;

    [ObservableProperty]
    private string _width;

    [ObservableProperty]
    private string _height;

    [ObservableProperty]
    private string _seed;

    [ObservableProperty]
    private string _seedPlaceholder;

    [ObservableProperty]
    private bool _enableGfpgan;

    [ObservableProperty]
    private string _gfpganStrength;

    [ObservableProperty]
    private string _gfpganStrengthPlaceholder;

    [ObservableProperty]
    private bool _enableUpscaling;

    [ObservableProperty]
    private string _upscaler; 
    
    [ObservableProperty]
    private string _upscaleLevel;

    [ObservableProperty]
    private string _upscaleSteps;

    [ObservableProperty]
    private string _upscaleStepsPlaceholder;

    [ObservableProperty]
    private bool _makeSeamless;

    public PromptSettingsPageViewModel(
        IStableDiffusionService stableDiffusionService,
        IPopupService popupService,
        ILoadingService loadingService) : base(loadingService)
    {
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));

        var upscaleLevelValues = new List<string>
        {
            "2","4"
        };

        AvailableUpscaleLevelValues = upscaleLevelValues;

        var defaultSettings = new PromptSettings();
        StepsPlaceholder = defaultSettings.Steps.ToString();
        CfgScalePlaceholder = defaultSettings.GuidanceScale.ToString();
        SeedPlaceholder = defaultSettings.Seed.ToString();
        GfpganStrengthPlaceholder = defaultSettings.GfpganStrength.ToString();
        UpscaleStepsPlaceholder = defaultSettings.UpscaleSteps.ToString();
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) ||
            promptSettings is not PromptSettings settings)
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

            var upscalers = await _stableDiffusionService.GetUpscalersAsync();

            AvailableUpscalerValues = upscalers.Select(u => u.Name).ToList();

            var models = await _stableDiffusionService.GetModelsAsync();

            AvailableModelValues = models;
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
        var result = await Shell.Current.DisplayAlertAsync("Confirm Reset", "Are you sure you would like to reset back to defaults?", "RESET", "Cancel");
        
        if (!result)
        {
            return;
        }

        var defaultSettings = new PromptSettings();
        CfgScale = defaultSettings.GuidanceScale.ToString();
        EnableGfpgan = defaultSettings.EnableGfpgan;
        EnableUpscaling = defaultSettings.EnableUpscaling;
        GfpganStrength = defaultSettings.GfpganStrength.ToString();
        Height = defaultSettings.Height.ToString();
        BatchCount = defaultSettings.BatchCount.ToString();
        BatchSize = defaultSettings.BatchSize.ToString();
        MakeSeamless = defaultSettings.Seamless == OnOff.on;
        Model = defaultSettings.Model;
        Sampler = defaultSettings.Sampler;
        Seed = defaultSettings.Seed.ToString();
        Steps = defaultSettings.Steps.ToString();
        Upscaler = defaultSettings.Upscaler.ToString();
        UpscaleLevel = defaultSettings.UpscaleLevel.ToString();
        UpscaleSteps = defaultSettings.UpscaleSteps.ToString();
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
        await LoadingService.ShowAsync("Saving...");

        try
        {
            mapPropertiesToSettings();

            await _stableDiffusionService.SaveSettingsAsync(_settings);

            var parameters = new Dictionary<string, object> { { NavigationParams.PromptSettings, _settings } };

            await Shell.Current.GoToAsync("..", parameters);
        }
        finally
        {
            await LoadingService.HideAsync();
        }
        
    }

    [RelayCommand]
    private async Task ShowResolutionSelect()
    {
        var parameters = new Dictionary<string, object> {
            { NavigationParams.Width, Width },
            { NavigationParams.Height, Height },
            { NavigationParams.InitImgString, _settings.InitImage }
        };

        var result = await _popupService.ShowPopupForResultAsync("ResolutionSelectPopup", parameters) as IDictionary<string, object>;

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
        Model = AvailableModelValues?.FirstOrDefault(m => m.Key == _settings.Model.Key);
        Sampler = _settings.Sampler;
        Seed = _settings.Seed.ToString();
        Steps = _settings.Steps.ToString();
        Upscaler = _settings.Upscaler;
        UpscaleLevel = _settings.UpscaleLevel == 0 ? "2" : _settings.UpscaleLevel.ToString();
        UpscaleSteps= _settings.UpscaleSteps.ToString();
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

        if (Model != null)
        {
            _settings.Model = (ModelViewModel)Model;
        }

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

        _settings.Upscaler = Upscaler;

        if (int.TryParse(UpscaleSteps, out var pUpscaleSteps))
        {
            _settings.UpscaleSteps = pUpscaleSteps;
        }

        if (double.TryParse(Width, out var pWidth))
        {
            _settings.Width = pWidth;
        }
    }
}
