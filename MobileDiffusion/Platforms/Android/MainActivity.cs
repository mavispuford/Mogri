using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;

namespace MobileDiffusion;

[Activity(Theme = "@style/Maui.SplashTheme", 
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask, 
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(new[] { Intent.ActionSend}, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "image/*")]
public class MainActivity : MauiAppCompatActivity
{
    public override void OnConfigurationChanged(Configuration newConfig)
    {
        try
        {
            base.OnConfigurationChanged(newConfig);
        }
        catch
        {
            // Dark/light theme changes can throw exceptions when using Popups
        }
    }

    protected override async void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var intent = Intent;

        checkForSharedImageIntent(intent);
    }

    protected override void OnNewIntent(Intent intent)
    {
        checkForSharedImageIntent(intent);

        base.OnNewIntent(intent);
    }

    private async void checkForSharedImageIntent(Intent intent)
    {
        if (intent?.Action == null ||
            intent.Action != Intent.ActionSend)
        {
            return;
        }

        if (intent.Extras.Get(Intent.ExtraStream) is Android.Net.Uri uri)
        {
            try
            {
                // Because this method is an async void, run this in a Task.Run for safer exception handling
                await Task.Run(async () =>
                {
                    var parameters = new Dictionary<string, object>
                    {
                        { NavigationParams.AppShareFileUri, uri.ToString() },
                        { NavigationParams.AppShareContentType, intent.Type }
                    };

                    // Outputting some things from the console because the app crashes when sharing files while the 
                    // debugger is attached. See: https://github.com/dotnet/maui/issues/10384
                    Console.WriteLine("ACTION_SEND Intent received. Navigating with these parameters:");
                    foreach (var param in parameters)
                    {
                        Console.WriteLine($"{param.Key}\t\t : {param.Value}");
                    }

                    var dispatcher = Dispatcher.GetForCurrentThread() ?? Microsoft.Maui.Controls.Application.Current.MainPage.Dispatcher;

                    await dispatcher.DispatchAsync(async () =>
                    {
                        await Shell.Current.GoToAsync("///MainPageTab", parameters);
                    });
                });
            }
            catch
            {
                // Do nothing.
            }
        }
    }
}
