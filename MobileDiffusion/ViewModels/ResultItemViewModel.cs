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
    private IRelayCommand applyQueryParamsFromResultItemCommand;

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

        ApplyQueryParamsFromResultItemCommand?.Execute(result);
    }
}
