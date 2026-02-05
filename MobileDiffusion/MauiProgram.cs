using CommunityToolkit.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;
using MobileDiffusion.Registrations;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Mopups.Hosting;
using Microsoft.Extensions.Logging;

namespace MobileDiffusion;

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
                fonts.AddFont("MaterialSymbolsRounded100.ttf", "MaterialIconsRound100");
                fonts.AddFont("MaterialSymbolsRounded300.ttf", "MaterialIconsRound300");
                fonts.AddFont("MaterialSymbolsRounded400.ttf", "MaterialIconsRound400");
                fonts.AddFont("MaterialSymbolsRounded700.ttf", "MaterialIconsRound700");
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
#endif

        return builder.Build();
    }

}
