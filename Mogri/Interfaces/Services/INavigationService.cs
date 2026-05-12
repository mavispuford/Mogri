namespace Mogri.Interfaces.Services;

/// <summary>
/// Wraps the route-oriented Shell navigation patterns used by the app.
/// </summary>
public interface INavigationService
{
    Task GoToAsync(string route);

    Task GoToAsync(string route, IDictionary<string, object> parameters);

    Task GoBackAsync();

    Task GoBackAsync(IDictionary<string, object> parameters);

    Task PopToRootAsync();

    Task PopToRootAndGoToAsync(string route, IDictionary<string, object> parameters);
}