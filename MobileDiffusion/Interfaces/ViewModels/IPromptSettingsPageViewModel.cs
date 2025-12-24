using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPromptSettingsPageViewModel : IPageViewModel
{
    ModelType SelectedModelType { get; set; }

    List<ModelType> AvailableModelTypes { get; set; }

    List<string> AvailableSchedulers { get; set; }

    string Scheduler { get; set; }

    List<IModelViewModel> AvailableModelValues { get; set; }

    List<string> AvailableSamplerValues { get; set; }

    List<string> AvailableUpscaleLevelValues { get; set; }

    List<string> AvailableUpscalerValues { get; set; }

    string BatchCount { get; set; }

    string BatchSize { get; set; }

    string Steps { get; set; }

    string StepsPlaceholder { get; set; }

    string CfgScale { get; set; }

    string CfgScalePlaceholder { get; set; }

    IModelViewModel Model { get; set; }

    string Sampler { get; set; }

    string Width { get; set; }
    
    string Height { get; set; }

    string Seed { get; set; }

    string SeedPlaceholder { get; set; }

    bool EnableGfpgan { get; set; }

    string GfpganStrength { get; set; }

    string GfpganStrengthPlaceholder { get; set; }
    
    bool EnableUpscaling { get; set; }

    string Upscaler { get; set; }

    string UpscaleLevel { get; set; }

    string UpscaleSteps { get; set; } 

    string UpscaleStepsPlaceholder { get; set; }

    bool MakeSeamless { get; set; }

    IAsyncRelayCommand ResetValuesCommand { get; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmSettingsCommand { get; }

    IAsyncRelayCommand ShowResolutionSelectCommand { get; }

    IAsyncRelayCommand SavePresetCommand { get; }

    IAsyncRelayCommand LoadPresetCommand { get; }

    List<string> AvailablePresets { get; set; }
}
