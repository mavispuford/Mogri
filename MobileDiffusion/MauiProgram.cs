using CommunityToolkit.Maui;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Services;

namespace MobileDiffusion;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
			.UseMauiCommunityToolkit()
			.RegisterViewModels()
			.RegisterViews();

		builder.Services.AddHttpClient();

		builder.Services.AddSingleton<IStableDiffusionService, LSteinStableDiffusionService>();

#if ANDROID
		builder.Services.AddSingleton<IFileService, FileService>();
#endif

        return builder.Build();
	}

}
