
namespace MobileDiffusion;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Global exception handling to unwrap TargetInvocationException and reveal the real cause
		AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
		{
			var exception = e.ExceptionObject as Exception;
			var innermost = GetInnermostException(exception);
			System.Diagnostics.Debug.WriteLine($"[UNHANDLED EXCEPTION] {innermost?.GetType().Name}: {innermost?.Message}");
			System.Diagnostics.Debug.WriteLine(innermost?.StackTrace);
		};

		TaskScheduler.UnobservedTaskException += (sender, e) =>
		{
			var innermost = GetInnermostException(e.Exception);
			System.Diagnostics.Debug.WriteLine($"[UNOBSERVED TASK EXCEPTION] {innermost?.GetType().Name}: {innermost?.Message}");
			System.Diagnostics.Debug.WriteLine(innermost?.StackTrace);
		};
	}

	private static Exception? GetInnermostException(Exception? exception)
	{
		while (exception?.InnerException != null)
		{
			exception = exception.InnerException;
		}
		return exception;
	}

    protected override Window CreateWindow(IActivationState activationState)
    {
        return new Window(new AppShell());
    }
}
