using CommunityToolkit.Maui.Alerts;
using Microsoft.Maui.ApplicationModel;
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

        return MainThread.InvokeOnMainThreadAsync(() => Toast.Make(message).Show());
    }
}