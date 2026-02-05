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
    public partial ImageSource ImageSource { get; set; }

    [ObservableProperty]
    public partial PromptSettings Settings { get; set; }

    [ObservableProperty]
    public partial ApiResponse ApiResponse { get; set; }

    [ObservableProperty]
    public partial string InternalUri { get; set; }

    [ObservableProperty]
    public partial IRelayCommand ApplyQueryParamsFromResultItemCommand { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool Failed { get; set; }

    public ResultItemViewModel(IPopupService popupService)
    {
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
    }

    [RelayCommand]
    private async Task Tapped()
    {
        if (ImageSource == default(ImageSource))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { NavigationParams.ImageResultItem, this }
        };

        var result = (await _popupService.ShowPopupForResultAsync("ResultItemPopup", parameters)) as Dictionary<string, object>;

        if (result == null)
        {
            return;
        }

        ApplyQueryParamsFromResultItemCommand?.Execute(result);
    }
}
