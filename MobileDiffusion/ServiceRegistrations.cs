using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Services;

namespace MobileDiffusion;

public static class ServiceRegistrations
{
    public static MauiAppBuilder RegisterServices(this MauiAppBuilder builder)
    {
        builder.Services.AddHttpClient();
        //builder.Services.AddHttpClient("Default").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
        //{
        //	Proxy = new WebProxy() { Address = new Uri("192.168.86.42:8888") }
        //});

        builder.Services.AddSingleton<IStableDiffusionService, LSteinStableDiffusionService>();
        builder.Services.AddSingleton<IPopupService, PopupService>();

#if ANDROID
        builder.Services.AddSingleton<IFileService, FileService>();
#endif

        return builder;
    }
}
