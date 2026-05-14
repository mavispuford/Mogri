using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Views.Popups;
using Mopups.Services;

namespace Mogri.Coordinators;

/// <summary>
/// Coordinates the loading popup lifecycle so viewmodels do not manage popup workflow state directly.
/// </summary>
public class LoadingCoordinator : ILoadingCoordinator
{
    private const string LoadingPopupName = "LoadingPopup";

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly IPopupService _popupService;

    public LoadingCoordinator(IPopupService popupService)
    {
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
    }

    public async Task HideAsync()
    {
        await _semaphore.WaitAsync();

        try
        {
            await _popupService.ClosePopupAsync(LoadingPopupName, null);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ShowAsync(string? loadingMessage = null)
    {
        await _semaphore.WaitAsync();

        try
        {
            if (MopupService.Instance.PopupStack.FirstOrDefault(p => p is LoadingPopup) is LoadingPopup loadingPopup)
            {
                loadingPopup.SetLoadingMessage(loadingMessage ?? string.Empty);

                return;
            }

            await _popupService.ShowPopupAsync(LoadingPopupName, new Dictionary<string, object>
            {
                { NavigationParams.LoadingMessage, loadingMessage ?? string.Empty }
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }
}