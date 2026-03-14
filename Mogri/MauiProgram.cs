using CommunityToolkit.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Mogri.Registrations;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Mopups.Hosting;
using Microsoft.Extensions.Logging;

namespace Mogri;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

                fonts.AddFont("Comfortaa-Light.ttf", "ComfortaaLight");
                fonts.AddFont("Comfortaa-Regular.ttf", "ComfortaaRegular");
                fonts.AddFont("Comfortaa-Medium.ttf", "ComfortaaMedium");
                fonts.AddFont("Comfortaa-SemiBold.ttf", "ComfortaaSemiBold");
                fonts.AddFont("Comfortaa-Bold.ttf", "ComfortaaBold");

                fonts.AddFont("MaterialSymbolsRounded100.ttf", "MaterialSymbolsRounded-Thin");
                fonts.AddFont("MaterialSymbolsRounded300.ttf", "MaterialSymbolsRounded-Light");
                fonts.AddFont("MaterialSymbolsRounded400.ttf", "MaterialSymbolsRounded-Regular");
                fonts.AddFont("MaterialSymbolsRounded700.ttf", "MaterialSymbolsRounded-Bold");
            })
            .UseMauiCommunityToolkit()
            .ConfigureMopups()
            .RegisterServices()
            .RegisterViewModels()
            .RegisterViews()
            .RegisterPopups();

        builder.Services.AddSingleton(DeviceDisplay.Current);

#if DEBUG

        builder.Logging.AddDebug();

#endif

#if ANDROID
        EntryHandler.Mapper.AppendToMapping("NoUnderline", (h, v) =>
        {
            h.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Colors.Transparent.ToPlatform());
        });

        EditorHandler.Mapper.AppendToMapping("NoUnderline", (h, v) =>
        {
            h.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Colors.Transparent.ToPlatform());
        });

        PickerHandler.Mapper.AppendToMapping("NoUnderline", (h, v) =>
        {
            h.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Colors.Transparent.ToPlatform());
        });
#elif IOS
        EntryHandler.Mapper.AppendToMapping("NoUnderline", (h, v) =>
        {
            h.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
        });

        EditorHandler.Mapper.AppendToMapping("NoUnderline", (h, v) =>
        {
            h.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
        });

        PickerHandler.Mapper.AppendToMapping("NoUnderline", (h, v) =>
        {
            h.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
        });
#endif

        return builder.Build();
    }

}
