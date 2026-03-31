
using CommunityToolkit.Mvvm.Messaging;

namespace Mogri;

/// <summary>
/// Application entry point. Configures global exception handling and window lifecycle events.
/// </summary>
public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Global exception handling to unwrap TargetInvocationException and reveal the real cause
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            var innermost = getInnermostException(exception);
            System.Diagnostics.Debug.WriteLine($"[UNHANDLED EXCEPTION] {innermost?.GetType().Name}: {innermost?.Message}");
            System.Diagnostics.Debug.WriteLine(innermost?.StackTrace);
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            var innermost = getInnermostException(e.Exception);
            System.Diagnostics.Debug.WriteLine($"[UNOBSERVED TASK EXCEPTION] {innermost?.GetType().Name}: {innermost?.Message}");
            System.Diagnostics.Debug.WriteLine(innermost?.StackTrace);
        };
    }

    private static Exception? getInnermostException(Exception? exception)
    {
        while (exception?.InnerException != null)
        {
            exception = exception.InnerException;
        }
        return exception;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        
        window.Activated += (s, e) => CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Messages.AppLifecycleMessage(true));
        window.Deactivated += (s, e) => CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Messages.AppLifecycleMessage(false));
        window.Stopped += (s, e) => CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Messages.AppStoppedMessage());
        
        return window;
    }
}
