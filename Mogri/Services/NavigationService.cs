using Microsoft.Maui.ApplicationModel;
using Mogri.Interfaces.Services;

namespace Mogri.Services;

/// <summary>
/// Adapter around the route-oriented Shell navigation patterns used by the app.
/// </summary>
public class NavigationService : INavigationService
{
    public Task GoToAsync(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);

        return MainThread.InvokeOnMainThreadAsync(() => getShell().GoToAsync(route));
    }

    public Task GoToAsync(string route, IDictionary<string, object> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentNullException.ThrowIfNull(parameters);

        return MainThread.InvokeOnMainThreadAsync(() => getShell().GoToAsync(route, parameters));
    }

    public Task GoBackAsync()
    {
        return GoToAsync("..");
    }

    public Task GoBackAsync(IDictionary<string, object> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return GoToAsync("..", parameters);
    }

    public Task PopToRootAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() => getShell().Navigation.PopToRootAsync());
    }

    public Task PopToRootAndGoToAsync(string route, IDictionary<string, object> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentNullException.ThrowIfNull(parameters);

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var shell = getShell();
            await shell.Navigation.PopToRootAsync();
            await shell.GoToAsync(route, parameters);
        });
    }

    private static Shell getShell()
    {
        return Shell.Current ?? throw new InvalidOperationException("Shell is not available.");
    }
}