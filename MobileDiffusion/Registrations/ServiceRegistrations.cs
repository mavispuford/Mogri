using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Services;

#if ANDROID
using MobileDiffusion.Platforms.Android.Services;
#endif

namespace MobileDiffusion.Registrations;

public static class ServiceRegistrations
{
    public static MauiAppBuilder RegisterServices(this MauiAppBuilder builder)
    {
        builder.Services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });/*.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                  Proxy = new WebProxy() { Address = new Uri("192.168.86.42:8888") }
        });*/

        // TODO - Add Polly using Microsoft.Extensions.Http.Polly

        builder.Services.AddSingleton<IStableDiffusionService, Automatic1111Service>();
        //builder.Services.AddSingleton<IStableDiffusionService, LSteinStableDiffusionService>();
        builder.Services.AddSingleton<IPopupService, PopupService>();
        builder.Services.AddSingleton<IImageService, ImageService>();

#if ANDROID
        builder.Services.AddSingleton<IFileService, FileService>();
#endif

        return builder;
    }
}
