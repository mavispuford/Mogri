using Microsoft.Maui.Dispatching;
using Mogri.Interfaces.Services;

namespace Mogri.Services;

/// <summary>
/// Adapter around a window-scoped MAUI dispatcher.
/// </summary>
public class MainThreadService : IMainThreadService
{
    private readonly IDispatcher _dispatcher;

    public MainThreadService(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public bool IsMainThread => !_dispatcher.IsDispatchRequired;

    public void BeginInvokeOnMainThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!_dispatcher.Dispatch(action))
        {
            throw new InvalidOperationException("Failed to dispatch work to the UI thread.");
        }
    }

    public Task InvokeOnMainThreadAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return _dispatcher.DispatchAsync(action);
    }

    public Task InvokeOnMainThreadAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return _dispatcher.DispatchAsync(action);
    }

    public Task<T> InvokeOnMainThreadAsync<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return _dispatcher.DispatchAsync(action);
    }

    public Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return _dispatcher.DispatchAsync(action);
    }
}