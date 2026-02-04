#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.ViewModels.Popups;
using MobileDiffusion.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class PopupBaseViewModel : BaseViewModel, IPopupBaseViewModel
{
    protected readonly IPopupService _popupService;

    [ObservableProperty]
    private double _contentOpacity = 1.0;

    [ObservableProperty]
    private Color _popupBackgroundColor;

    public PopupBaseViewModel(IPopupService popupService)
    {
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));

        if (Application.Current?.Resources != null && Application.Current.Resources.TryGetValue("BlackSeventyThreePercent", out var bgColor))
        {
            PopupBackgroundColor = (Color)bgColor;
        }
        else
        {
            PopupBackgroundColor = Color.FromArgb("BB000000");
        }
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
