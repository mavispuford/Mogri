using CommunityToolkit.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;
using MobileDiffusion.Registrations;
using Microsoft.Maui.Platform;

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
				fonts.AddFont("MaterialIconsRound-Regular.otf", "MaterialIconsRoundRegular");
			})
			.UseMauiCommunityToolkit()
			.RegisterServices()
			.RegisterViewModels()
			.RegisterViews()
			.RegisterPopups();

        builder.Services.AddSingleton(DeviceDisplay.Current);

#if ANDROID
		Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (h, v) => { 
			h.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Colors.Transparent.ToPlatform());
		});

        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoUnderline", (h, v) => {
            h.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Colors.Transparent.ToPlatform());
        });
#endif

        return builder.Build();
	}

}
