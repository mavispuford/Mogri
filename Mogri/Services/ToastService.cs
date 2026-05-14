using CommunityToolkit.Maui.Alerts;
using Microsoft.Maui.Dispatching;
using Mogri.Interfaces.Services;

namespace Mogri.Services;

/// <summary>
/// Adapter around CommunityToolkit toast notifications.
/// </summary>
public class ToastService : IToastService
{
    public Task ShowAsync(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var shell = getShell();

        return shell.Dispatcher.DispatchAsync(() => Toast.Make(message).Show());
    }

    private static Shell getShell()
    {
        return Shell.Current ?? throw new InvalidOperationException("Shell is not available.");
    }
}