using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Enums;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using Mogri.Models;

namespace Mogri.ViewModels;

public partial class GenerationSettingsPageViewModel : PageViewModel, IGenerationSettingsPageViewModel
{
    private readonly IImageGenerationService _stableDiffusionService;
    private readonly IPopupService _popupService;
    private readonly IPresetService _presetService;

    private PromptSettings _settings = new();

    [ObservableProperty]
    public partial List<IModelViewModel> AvailableModelValues { get; set; } = new();

    [ObservableProperty]
    public partial List<string> AvailableSamplerValues { get; set; } = new();

    [ObservableProperty]
    public partial List<string> AvailableUpscaleLevelValues { get; set; } = new();

    [ObservableProperty]
    public partial List<string> AvailableUpscalerValues { get; set; } = new();

    [ObservableProperty]
    public partial string? BatchCount { get; set; }

    [ObservableProperty]
    public partial string? BatchSize { get; set; }

    [ObservableProperty]
    public partial string? Steps { get; set; }

    [ObservableProperty]
    public partial string? StepsPlaceholder { get; set; }

    [ObservableProperty]
    public partial string? CfgScale { get; set; }

    [ObservableProperty]
    public partial string? CfgScalePlaceholder { get; set; }

    [ObservableProperty]
    public partial string? DistilledCfgScale { get; set; }

    [ObservableProperty]
    public partial string? DistilledCfgScalePlaceholder { get; set; }

    [ObservableProperty]
    public partial bool IsDistilledCfgScaleVisible { get; set; }

    [ObservableProperty]
    public partial IModelViewModel? Model { get; set; }

    [ObservableProperty]
    public partial string? Sampler { get; set; }

    [ObservableProperty]
    public partial string? Width { get; set; }

    [ObservableProperty]
    public partial string? Height { get; set; }

    [ObservableProperty]
    public partial string? Seed { get; set; }

    [ObservableProperty]
    public partial string? SeedPlaceholder { get; set; }

    [ObservableProperty]
    public partial bool EnableUpscaling { get; set; }

    [ObservableProperty]
    public partial string? Upscaler { get; set; }

    [ObservableProperty]
    public partial string? UpscaleLevel { get; set; }

    [ObservableProperty]
    public partial string? UpscaleSteps { get; set; }

    [ObservableProperty]
    public partial string? UpscaleStepsPlaceholder { get; set; }

    [ObservableProperty]
    public partial bool EnableTiling { get; set; }

    [ObservableProperty]
    public partial bool IsSeamlessVisible { get; set; }

    [ObservableProperty]
    public partial ModelType SelectedModelType { get; set; }

    [ObservableProperty]
    public partial List<ModelType> AvailableModelTypes { get; set; } = Enum.GetValues(typeof(ModelType)).Cast<ModelType>().ToList();

    [ObservableProperty]
    public partial List<string> AvailableSchedulers { get; set; } = new();

    [ObservableProperty]
    public partial string? Scheduler { get; set; }

    [ObservableProperty]
    public partial List<string> AvailableVaes { get; set; } = new();

    [ObservableProperty]
    public partial string? Vae { get; set; }

    [ObservableProperty]
    public partial List<string> AvailableTextEncoders { get; set; } = new();

    [ObservableProperty]
    public partial string? TextEncoder { get; set; }

    [ObservableProperty]
    public partial List<string> AvailablePresets { get; set; } = new();

    [ObservableProperty]
    public partial BackendCapabilities CurrentCapabilities { get; set; } = BackendCapabilities.None;

    private bool _isInitializing;

    async partial void OnSelectedModelTypeChanged(ModelType value)
    {
        if (_isInitializing) return;

        try
        {
            var profile = GenerationProfile.GetDefault(value);

            Steps = profile.DefaultSteps.ToString();
            CfgScale = profile.DefaultCfg.ToString();
            DistilledCfgScale = profile.DefaultDistilledCfg?.ToString();

            var defaultWidth = profile.DefaultWidth.ToString();
            var defaultHeight = profile.DefaultHeight.ToString();

            if (Width != defaultWidth || Height != defaultHeight)
            {
                var resChangeMessage = $"Would you like keep the resolution at {Width}x{Height} or CHANGE it to {defaultWidth}x{defaultHeight}?";
                var resChangeResult = await _popupService.DisplayAlertAsync("Confirm Resolution Change", resChangeMessage, "CHANGE", "Keep");

                if (resChangeResult)
                {
                    Width = defaultWidth;
                    Height = defaultHeight;
                }
            }

            Sampler = profile.DefaultSampler;
            Scheduler = profile.DefaultScheduler;

            if (!string.IsNullOrEmpty(profile.DefaultVae))
            {
                var match = AvailableVaes.FirstOrDefault(v => v.Contains(profile.DefaultVae, StringComparison.OrdinalIgnoreCase));
                if (match != null) Vae = match;
            }
            else
            {
                Vae = "Automatic";
            }

            if (!string.IsNullOrEmpty(profile.DefaultTextEncoder))
            {
                var match = AvailableTextEncoders.FirstOrDefault(v => v.Contains(profile.DefaultTextEncoder, StringComparison.OrdinalIgnoreCase));
                if (match != null) TextEncoder = match;
            }
            else
            {
                TextEncoder = "None";
            }

            IsDistilledCfgScaleVisible = value == ModelType.ZImageTurbo || value == ModelType.Flux;
            IsSeamlessVisible = CurrentCapabilities.SupportsSeamless && value == ModelType.SDXL;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnSelectedModelTypeChanged: {ex}");
        }
    }

    partial void OnModelChanged(IModelViewModel? value)
    {
        if (_isInitializing || value == null) return;

        string savedVae;
        if (Preferences.Default.ContainsKey($"Vae_{value.Key}"))
        {
            savedVae = Preferences.Default.Get($"Vae_{value.Key}", "Automatic");
        }
        else
        {
            var profile = GenerationProfile.GetDefault(SelectedModelType);
            savedVae = AvailableVaes.FirstOrDefault(v => !string.IsNullOrEmpty(profile.DefaultVae) && v.Contains(profile.DefaultVae, StringComparison.OrdinalIgnoreCase)) ?? "Automatic";
        }

        if (AvailableVaes.Contains(savedVae))
        {
            Vae = savedVae;
        }

        string savedTextEncoder;
        if (Preferences.Default.ContainsKey($"TextEncoder_{value.Key}"))
        {
            savedTextEncoder = Preferences.Default.Get($"TextEncoder_{value.Key}", "None");
        }
        else
        {
            var profile = GenerationProfile.GetDefault(SelectedModelType);
            savedTextEncoder = AvailableTextEncoders.FirstOrDefault(v => !string.IsNullOrEmpty(profile.DefaultTextEncoder) && v.Contains(profile.DefaultTextEncoder, StringComparison.OrdinalIgnoreCase)) ?? "None";
        }

        if (AvailableTextEncoders.Contains(savedTextEncoder))
        {
            TextEncoder = savedTextEncoder;
        }
    }

    public GenerationSettingsPageViewModel(
        IImageGenerationService stableDiffusionService,
        IPopupService popupService,
        ILoadingService loadingService,
        IPresetService presetService) : base(loadingService)
    {
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        _presetService = presetService ?? throw new ArgumentNullException(nameof(presetService));

        var upscaleLevelValues = new List<string>
        {
            "2","4"
        };

        AvailableUpscaleLevelValues = upscaleLevelValues;

        var defaultSettings = new PromptSettings();
        StepsPlaceholder = defaultSettings.Steps.ToString();
        CfgScalePlaceholder = defaultSettings.GuidanceScale.ToString();
        DistilledCfgScalePlaceholder = defaultSettings.DistilledCfgScale?.ToString();
        SeedPlaceholder = defaultSettings.Seed.ToString();
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
            CurrentCapabilities = _stableDiffusionService.Capabilities;

            var samplers = await _stableDiffusionService.GetSamplersAsync();

            AvailableSamplerValues = samplers.Select(s => s.Key).ToList();

            var upscalers = await _stableDiffusionService.GetUpscalersAsync();

            AvailableUpscalerValues = upscalers.Select(u => u.Name).ToList();

            var models = await _stableDiffusionService.GetModelsAsync();

            AvailableModelValues = models;

            var schedulers = await _stableDiffusionService.GetSchedulersAsync();

            AvailableSchedulers = schedulers;

            if (CurrentCapabilities.SupportsVaes)
            {
                AvailableVaes = await _stableDiffusionService.GetVaesAsync();
            }

            if (CurrentCapabilities.SupportsTextEncoders)
            {
                AvailableTextEncoders = await _stableDiffusionService.GetTextEncodersAsync();
            }

            AvailablePresets = await _presetService.GetPresetsAsync();
        }
        catch (Exception ex)
        {
            await _popupService.DisplayAlertAsync("Error", $"Failed to initialize settings: {ex.Message}", "OK");
        }

        mapSettingsToProperties();
    }

    [RelayCommand]
    private async Task ResetValues()
    {
        var result = await _popupService.DisplayAlertAsync("Confirm Reset", "Are you sure you would like to reset back to defaults?", "RESET", "Cancel");

        if (!result)
        {
            return;
        }

        var defaultSettings = new PromptSettings();
        CfgScale = defaultSettings.GuidanceScale.ToString();
        EnableUpscaling = defaultSettings.EnableUpscaling;
        Height = defaultSettings.Height.ToString();
        BatchCount = defaultSettings.BatchCount.ToString();
        BatchSize = defaultSettings.BatchSize.ToString();
        EnableTiling = defaultSettings.EnableTiling;
        Model = defaultSettings.Model;
        Sampler = defaultSettings.Sampler;
        Scheduler = defaultSettings.Scheduler;

        var profile = GenerationProfile.GetDefault(SelectedModelType);
        Vae = AvailableVaes.FirstOrDefault(v => !string.IsNullOrEmpty(profile.DefaultVae) && v.Contains(profile.DefaultVae, StringComparison.OrdinalIgnoreCase)) ?? "Automatic";
        TextEncoder = AvailableTextEncoders.FirstOrDefault(v => !string.IsNullOrEmpty(profile.DefaultTextEncoder) && v.Contains(profile.DefaultTextEncoder, StringComparison.OrdinalIgnoreCase)) ?? "None";

        Seed = defaultSettings.Seed.ToString();
        Steps = defaultSettings.Steps.ToString();
        Upscaler = defaultSettings.Upscaler?.ToString();
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

            var currentModel = await _stableDiffusionService.GetSelectedModelAsync();

            var modelChangeResult = currentModel?.Key == null;

            if (currentModel != null &&
                _settings.Model != null &&
                _settings.Model.Key != currentModel.Key)
            {
                var modelChangeMessage = $"Would you like keep current model ({currentModel.DisplayName}) or CHANGE it to \"{_settings.Model.DisplayName}\"?";

                modelChangeResult = await _popupService.DisplayAlertAsync("Confirm Model Change", modelChangeMessage, "CHANGE", "Keep");
            }
            else if (currentModel != null && _settings.Model != null && _settings.Model.Key == currentModel.Key)
            {
                modelChangeResult = true;
            }

            if (!modelChangeResult)
            {
                // Keep currently selected model
                _settings.Model = currentModel;
            }

            if (_settings.Model != null)
            {
                if (!string.IsNullOrEmpty(_settings.Vae))
                {
                    Preferences.Default.Set($"Vae_{_settings.Model.Key}", _settings.Vae);
                }
                if (!string.IsNullOrEmpty(_settings.TextEncoder))
                {
                    Preferences.Default.Set($"TextEncoder_{_settings.Model.Key}", _settings.TextEncoder);
                }
            }

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
        var initImgString = !string.IsNullOrEmpty(_settings.InitImageThumbnail) ? _settings.InitImageThumbnail : _settings.InitImage;

        var parameters = new Dictionary<string, object> {
            { NavigationParams.Width, Width ?? "512" },
            { NavigationParams.Height, Height ?? "512" },
            { NavigationParams.InitImgString, initImgString ?? string.Empty }
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
        _isInitializing = true;
        try
        {
            CfgScale = _settings.GuidanceScale.ToString();
            DistilledCfgScale = _settings.DistilledCfgScale?.ToString();
            EnableUpscaling = _settings.EnableUpscaling;
            Height = _settings.Height.ToString();
            BatchCount = _settings.BatchCount.ToString();
            BatchSize = _settings.BatchSize.ToString();
            EnableTiling = _settings.EnableTiling;
            Model = _settings.Model != null
                ? AvailableModelValues?.FirstOrDefault(m => m.Key == _settings.Model.Key)
                : null;
            Sampler = _settings.Sampler;
            Scheduler = _settings.Scheduler;
            
            if (Model != null)
            {
                if (_settings.Vae != null)
                {
                    Vae = _settings.Vae;
                }
                else if (Preferences.Default.ContainsKey($"Vae_{Model.Key}"))
                {
                    Vae = Preferences.Default.Get($"Vae_{Model.Key}", "Automatic");
                }
                else
                {
                    var profile = GenerationProfile.GetDefault(_settings.ModelType);
                    Vae = AvailableVaes.FirstOrDefault(v => !string.IsNullOrEmpty(profile.DefaultVae) && v.Contains(profile.DefaultVae, StringComparison.OrdinalIgnoreCase)) ?? "Automatic";
                }

                if (_settings.TextEncoder != null)
                {
                    TextEncoder = _settings.TextEncoder;
                }
                else if (Preferences.Default.ContainsKey($"TextEncoder_{Model.Key}"))
                {
                    TextEncoder = Preferences.Default.Get($"TextEncoder_{Model.Key}", "None");
                }
                else
                {
                    var profile = GenerationProfile.GetDefault(_settings.ModelType);
                    TextEncoder = AvailableTextEncoders.FirstOrDefault(v => !string.IsNullOrEmpty(profile.DefaultTextEncoder) && v.Contains(profile.DefaultTextEncoder, StringComparison.OrdinalIgnoreCase)) ?? "None";
                }
            }
            else
            {
                Vae = _settings.Vae;
                TextEncoder = _settings.TextEncoder;
            }

            Seed = _settings.Seed.ToString();
            SelectedModelType = _settings.ModelType;
            Steps = _settings.Steps.ToString();
            Upscaler = _settings.Upscaler?.ToString();
            UpscaleLevel = _settings.UpscaleLevel == 0 ? "2" : _settings.UpscaleLevel.ToString();
            UpscaleSteps = _settings.UpscaleSteps.ToString();
            Width = _settings.Width.ToString();

            IsDistilledCfgScaleVisible = SelectedModelType == ModelType.ZImageTurbo || SelectedModelType == ModelType.Flux;
            IsSeamlessVisible = CurrentCapabilities.SupportsSeamless && SelectedModelType == ModelType.SDXL;
        }
        finally
        {
            _isInitializing = false;
        }
    }

    [RelayCommand]
    private async Task SavePreset()
    {
        var name = await _popupService.DisplayPromptAsync("Save Preset", "Enter a name for this preset:");
        if (string.IsNullOrWhiteSpace(name)) return;

        mapPropertiesToSettings();
        await _presetService.SavePresetAsync(name, _settings);
        AvailablePresets = await _presetService.GetPresetsAsync();
        await _popupService.DisplayAlertAsync("Success", "Preset saved successfully.", "OK");
    }

    [RelayCommand]
    private async Task LoadPreset()
    {
        if (AvailablePresets == null || !AvailablePresets.Any())
        {
            await _popupService.DisplayAlertAsync("No Presets", "No presets available to load.", "OK");
            return;
        }

        var presets = AvailablePresets.ToArray();
        var name = await _popupService.DisplayActionSheetAsync("Load Preset", "Cancel", null, presets);
        if (string.IsNullOrEmpty(name) || name == "Cancel") return;

        var settings = await _presetService.LoadPresetAsync(name);
        if (settings != null && _settings != null)
        {

            // Store Prompt Page property values
            var currentPrompt = _settings.Prompt;
            var currentNegativePrompt = _settings.NegativePrompt;
            var currentPromptStyles = _settings.PromptStyles;
            var currentLoras = _settings.Loras;

            // Store Image to Image Settings Page property values
            var currentInitImage = _settings.InitImage;
            var currentMask = _settings.Mask;

            _settings = settings;

            #region Prompt Page

            // Set the Prompt Page properties back if they were previously set

            if (!string.IsNullOrEmpty(currentPrompt))
            {
                _settings.Prompt = currentPrompt;
            }

            if (!string.IsNullOrEmpty(currentNegativePrompt))
            {
                _settings.NegativePrompt = currentNegativePrompt;
            }

            if (currentPromptStyles.Any())
            {
                _settings.PromptStyles = currentPromptStyles;
            }

            if (currentLoras.Any())
            {
                _settings.Loras = currentLoras;
            }

            #endregion

            #region Image to Image Settings

            // Set the init image and mask if it was already set

            if (!string.IsNullOrEmpty(currentInitImage))
            {
                _settings.InitImage = currentInitImage;
            }

            if (!string.IsNullOrEmpty(currentMask))
            {
                _settings.Mask = currentMask;
            }

            #endregion

            mapSettingsToProperties();
        }
    }

    private void mapPropertiesToSettings()
    {
        if (double.TryParse(CfgScale, out var pCfgScale) ||
            double.TryParse(CfgScalePlaceholder, out pCfgScale))
        {
            _settings.GuidanceScale = pCfgScale;
        }

        if (double.TryParse(DistilledCfgScale, out var pDistilledCfgScale) ||
            double.TryParse(DistilledCfgScalePlaceholder, out pDistilledCfgScale))
        {
            _settings.DistilledCfgScale = pDistilledCfgScale;
        }

        _settings.EnableUpscaling = EnableUpscaling;

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

        _settings.EnableTiling = EnableTiling;

        if (Model != null)
        {
            _settings.Model = Model;
        }

        _settings.Sampler = Sampler ?? "Euler a";

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

        _settings.ModelType = SelectedModelType;
        _settings.Scheduler = Scheduler;
        _settings.Vae = Vae;
        _settings.TextEncoder = TextEncoder;
    }

}