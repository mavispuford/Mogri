namespace Mogri.Interfaces.Services;

/// <summary>
/// Wraps the main-thread dispatch primitives used by shared layers.
/// </summary>
public interface IMainThreadService
{
    bool IsMainThread { get; }

    void BeginInvokeOnMainThread(Action action);

    Task InvokeOnMainThreadAsync(Action action);

    Task InvokeOnMainThreadAsync(Func<Task> action);

    Task<T> InvokeOnMainThreadAsync<T>(Func<T> action);

    Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> action);
}