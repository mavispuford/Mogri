using Microsoft.Maui.Dispatching;
using Mogri.Interfaces.Services;

namespace Mogri.Services;

/// <summary>
/// Adapter around MAUI main-thread helpers.
/// </summary>
public class MainThreadService : IMainThreadService
{
    public bool IsMainThread => tryGetDispatcher() is { IsDispatchRequired: false };

    public void BeginInvokeOnMainThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = getDispatcher();

        if (!dispatcher.IsDispatchRequired)
        {
            action();
            return;
        }

        if (!dispatcher.Dispatch(action))
        {
            throw new InvalidOperationException("Failed to dispatch work to the UI thread.");
        }
    }

    public Task InvokeOnMainThreadAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = getDispatcher();

        if (!dispatcher.IsDispatchRequired)
        {
            action();
            return Task.CompletedTask;
        }

        return DispatcherExtensions.DispatchAsync(dispatcher, action);
    }

    public Task InvokeOnMainThreadAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = getDispatcher();

        if (!dispatcher.IsDispatchRequired)
        {
            return action();
        }

        return DispatcherExtensions.DispatchAsync(dispatcher, action);
    }

    public Task<T> InvokeOnMainThreadAsync<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = getDispatcher();

        if (!dispatcher.IsDispatchRequired)
        {
            return Task.FromResult(action());
        }

        return DispatcherExtensions.DispatchAsync(dispatcher, action);
    }

    public Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = getDispatcher();

        if (!dispatcher.IsDispatchRequired)
        {
            return action();
        }

        return DispatcherExtensions.DispatchAsync(dispatcher, action);
    }

    private static IDispatcher getDispatcher()
    {
        return tryGetDispatcher() ?? throw new InvalidOperationException("No UI dispatcher is available.");
    }

    private static IDispatcher? tryGetDispatcher()
    {
        return Dispatcher.GetForCurrentThread()
            ?? Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page?.Dispatcher;
    }
}