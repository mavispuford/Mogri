using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;

namespace MobileDiffusion;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
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
}
