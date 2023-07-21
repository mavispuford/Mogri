using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.ViewModels;

public partial class ResultItemViewModel : BaseViewModel, IResultItemViewModel
{
    private readonly IPopupService _popupService;

    [ObservableProperty]
    private ImageSource imageSource;

    [ObservableProperty]
    private Settings settings;

    [ObservableProperty]
    private ApiResponse apiResponse;

    [ObservableProperty]
    private string internalUri;

    [ObservableProperty]
    private IRelayCommand setSeedCommand;

    [ObservableProperty]
    private IRelayCommand setSettingsCommand;

    [ObservableProperty]
    private IRelayCommand setInitImageCommand;
    
    [ObservableProperty]
    private IRelayCommand setCanvasImageCommand;

    [ObservableProperty]
    private bool isLoading = true;

    [ObservableProperty]
    private bool failed;

    public ResultItemViewModel(IPopupService popupService)
    {
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
    }

    [RelayCommand]
    private async Task Tapped()
    {
        if (imageSource == default(ImageSource))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { NavigationParams.ImageResultItem, this }
        };

        var result = (await _popupService.ShowPopupAsync("ResultItemPopup", parameters)) as Dictionary<string, object>;

        if (result == null)
        {
            return;
        }

        if (result.TryGetValue(NavigationParams.PromptSettings, out var settingsParam) &&
            settingsParam is Settings settings)
        {
            SetSettingsCommand?.Execute(settings);
        }
        else if (result.TryGetValue(NavigationParams.Seed, out var seedParam) &&
            seedParam is long seed)
        {
            SetSeedCommand?.Execute(seed);
        }
        else if (result.TryGetValue(NavigationParams.InitImgString, out var initImgParam) && 
            initImgParam is string initImage)
        {
            SetInitImageCommand?.Execute(initImage);
        }
        else if (result.TryGetValue(NavigationParams.CanvasImageString, out var canvasImgParam) &&
            canvasImgParam is string canvasImage)
        {
            SetCanvasImageCommand?.Execute(canvasImage);
        }
    }
}
