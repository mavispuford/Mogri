#nullable enable

using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.ViewModels;

public partial class PopupBaseViewModel : BaseViewModel, IPopupBaseViewModel
{
    private readonly IPopupService _popupService;

    public PopupBaseViewModel(IPopupService popupService)
    {
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
    }

    public virtual void ApplyQueryAttributes(IDictionary<string, object> query)
    {
    }

    public virtual Task OnAppearingAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnDisappearingAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnNavigatedFromAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnNavigatedToAsync()
    {
        return Task.CompletedTask;
    }

    protected Task ClosePopupAsync(object? result = null)
    {
        return _popupService.ClosePopupAsync(this, result);
    }

    public virtual async void OnBackButtonPressed()
    {
        try
        {
            await Task.Run(async () =>
            {
                await ClosePopupAsync();
            });
        }
        catch
        {
            // TODO - Handle this
        }
    } 
}