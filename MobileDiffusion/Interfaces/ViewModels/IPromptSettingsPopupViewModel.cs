using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPromptSettingsPopupViewModel : IPopupBaseViewModel
{
    List<string> AvailableWidthValues { get; set; }

    List<string> AvailableHeightValues { get; set; }

    List<string> AvailableSamplerValues { get; set; }

    List<string> AvailableUpscaleLevelValues { get; set; }

    string ImageCount { get; set; }

    string ImageCountPlaceholder { get; set; }

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

    IRelayCommand ResetValuesCommand { get; }

    IRelayCommand CancelCommand { get; }

    IRelayCommand ConfirmSettingsCommand { get; }
}
