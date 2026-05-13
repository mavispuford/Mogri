using Microsoft.Maui.Dispatching;
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

        var shell = getShell();

        return shell.Dispatcher.DispatchAsync(() => shell.GoToAsync(route));
    }

    public Task GoToAsync(string route, IDictionary<string, object> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentNullException.ThrowIfNull(parameters);

        var shell = getShell();

        return shell.Dispatcher.DispatchAsync(() => shell.GoToAsync(route, parameters));
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
        var shell = getShell();

        return shell.Dispatcher.DispatchAsync(() => shell.Navigation.PopToRootAsync());
    }

    public Task PopToRootAndGoToAsync(string route, IDictionary<string, object> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentNullException.ThrowIfNull(parameters);

        var shell = getShell();

        return shell.Dispatcher.DispatchAsync(async () =>
        {
            await shell.Navigation.PopToRootAsync();
            await shell.GoToAsync(route, parameters);
        });
    }

    private static Shell getShell()
    {
        return Shell.Current ?? throw new InvalidOperationException("Shell is not available.");
    }
}