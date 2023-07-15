using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class PromptStyleInfoPopupViewModel : PopupBaseViewModel, IPromptStyleInfoPopupViewModel
{
    [ObservableProperty]
    private IPromptStyleViewModel _promptStyle;

    public PromptStyleInfoPopupViewModel(IPopupService popupService) : base(popupService)
    {}

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.PromptStyle, out var promptStyleParam) &&
            promptStyleParam is IPromptStyleViewModel promptStyle)
        {
            PromptStyle = promptStyle;
        }
        else
        {
            ClosePopup();
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    [RelayCommand]
    private void Close()
    {
        ClosePopup();
    }
}
