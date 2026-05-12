using Microsoft.Maui.ApplicationModel;
using Mogri.Interfaces.Services;

namespace Mogri.Services;

/// <summary>
/// Adapter around MAUI main-thread helpers.
/// </summary>
public class MainThreadService : IMainThreadService
{
    public bool IsMainThread => MainThread.IsMainThread;

    public void BeginInvokeOnMainThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        MainThread.BeginInvokeOnMainThread(action);
    }

    public Task InvokeOnMainThreadAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return MainThread.InvokeOnMainThreadAsync(action);
    }

    public Task InvokeOnMainThreadAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return MainThread.InvokeOnMainThreadAsync(action);
    }

    public Task<T> InvokeOnMainThreadAsync<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return MainThread.InvokeOnMainThreadAsync(action);
    }

    public Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return MainThread.InvokeOnMainThreadAsync(action);
    }
}