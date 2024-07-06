using MobileDiffusion.Interfaces.Services;

namespace MobileDiffusion.Services;

public class LoadingService : ILoadingService
{
    private const string _loadingPopupName = "LoadingPopup";

    private readonly SemaphoreSlim _semaphore = new(1,1);
    private readonly IPopupService _popupService;

    public LoadingService(IPopupService popupService)
    {
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
    }

    public async Task HideAsync()
    {
        await _semaphore.WaitAsync();

        try
        {
            await _popupService.ClosePopupAsync(_loadingPopupName, null);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ShowAsync(string loadingMessage = null)
    {
        await _semaphore.WaitAsync();

        try
        {
            await _popupService.ShowPopupAsync(_loadingPopupName, new Dictionary<string, object>()
            {
                { NavigationParams.LoadingMessage, loadingMessage }
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
