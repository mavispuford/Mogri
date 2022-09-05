#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;

[INotifyPropertyChanged]
public partial class PopupBaseViewModel : IPopupBaseViewModel
{
    private readonly IPopupService _popupService;

    public PopupBaseViewModel(IPopupService popupService)
    {
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
    }

    public virtual void ApplyQueryAttributes(IDictionary<string, object> query)
    {
    }

    protected void ClosePopup(object? result)
    {
        _popupService.ClosePopup(this, result);
    }
}