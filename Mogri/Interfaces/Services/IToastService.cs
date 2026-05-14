namespace Mogri.Interfaces.Services;

/// <summary>
/// Wraps lightweight toast notifications.
/// </summary>
public interface IToastService
{
    Task ShowAsync(string message);
}