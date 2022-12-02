using CommunityToolkit.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;
using MobileDiffusion.Registrations;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Handlers;

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

        // Workaround for bug:
        // https://github.com/dotnet/maui/issues/9011#issuecomment-1333534818
        ImageHandler.Mapper.PrependToMapping("Fix Bug 9011", (h, v) =>
		{
            h.PlatformView?.Clear();
        });
#endif

        return builder.Build();
	}

}
