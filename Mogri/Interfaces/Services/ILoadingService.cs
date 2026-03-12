namespace Mogri.Interfaces.Services;

public interface ILoadingService
{
    public Task ShowAsync(string? loadingMessage = null);

    public Task HideAsync();
}
