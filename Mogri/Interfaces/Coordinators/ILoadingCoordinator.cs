namespace Mogri.Interfaces.Coordinators;

/// <summary>
/// Coordinates loading-overlay presentation without exposing popup implementation details to callers.
/// </summary>
public interface ILoadingCoordinator
{
    Task ShowAsync(string? loadingMessage = null);

    Task HideAsync();
}