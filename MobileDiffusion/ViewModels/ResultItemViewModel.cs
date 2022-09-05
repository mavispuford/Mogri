using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models.LStein;

namespace MobileDiffusion.ViewModels;

public partial class ResultItemViewModel : BaseViewModel, IResultItemViewModel
{
    private readonly IPopupService _popupService;

    [ObservableProperty]
    private ImageSource imageSource;

    [ObservableProperty]
    private LSteinResponseItem config;

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

        var parameters = new Dictionary<string, object>()
        {
            { NavigationParams.ImageResultItem, this }
        };

        var result = await _popupService.ShowPopupAsync("ResultItemPopup", parameters);
    }
}
