using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class ResultItemPopupViewModel : PopupBaseViewModel, IResultItemPopupViewModel
{
    [ObservableProperty]
    private IResultItemViewModel resultItem;

    public ResultItemPopupViewModel(IPopupService popupService) : base(popupService)
    {
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.ImageResultItem, out var imageResultParam) &&
            imageResultParam is IResultItemViewModel imageResultItem)
        {
            ResultItem = imageResultItem;
        }
        else
        {
            ClosePopup();
        }
    }

    [RelayCommand]
    private void Close()
    {
        ClosePopup();
    }
}
