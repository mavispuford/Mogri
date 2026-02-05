using Android.App;
using Android.Runtime;

namespace MobileDiffusion;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
        AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
        {
            if (args.Exception.GetBaseException() is ObjectDisposedException)
            {
                args.Handled = true;
            }
        };
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
