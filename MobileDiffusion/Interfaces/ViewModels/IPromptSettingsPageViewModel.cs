using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPromptSettingsPageViewModel : IBaseViewModel
{
    List<string> AvailableWidthValues { get; set; }

    List<string> AvailableHeightValues { get; set; }

    List<string> AvailableSamplerValues { get; set; }

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

    IRelayCommand ResetValuesCommand { get; }

    IRelayCommand ConfirmSettingsCommand { get; }
}
