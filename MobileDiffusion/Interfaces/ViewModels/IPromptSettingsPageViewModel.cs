using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPromptSettingsPageViewModel : IPageViewModel
{
    List<string> AvailableWidthValues { get; set; }

    List<string> AvailableHeightValues { get; set; }

    List<string> AvailableSamplerValues { get; set; }

    List<string> AvailableUpscaleLevelValues { get; set; }
    
    string BatchCount { get; set; }

    string BatchSize { get; set; }

    string Steps { get; set; }

    string StepsPlaceholder { get; set; }

    string CfgScale { get; set; }

    string CfgScalePlaceholder { get; set; }

    string Sampler { get; set; }

    string Width { get; set; }
    
    string Height { get; set; }

    string Seed { get; set; }

    string SeedPlaceholder { get; set; }

    bool EnableGfpgan { get; set; }

    string GfpganStrength { get; set; }

    string GfpganStrengthPlaceholder { get; set; }
    
    bool EnableUpscaling { get; set; }

    string UpscaleLevel { get; set; }

    string UpscaleStrength { get; set; } 

    string UpscaleStrengthPlaceholder { get; set; }

    bool MakeSeamless { get; set; }

    IAsyncRelayCommand ResetValuesCommand { get; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmSettingsCommand { get; }
}
