using CommunityToolkit.Maui;

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
                fonts.AddFont("MaterialIconsRound-Regular.otf", "MaterialIconsRoundRegular");
            })
			.UseMauiCommunityToolkit()
			.RegisterServices()
			.RegisterViewModels()
			.RegisterViews()
			.RegisterPopups();

        builder.Services.AddSingleton(DeviceDisplay.Current);

        return builder.Build();
	}

}
