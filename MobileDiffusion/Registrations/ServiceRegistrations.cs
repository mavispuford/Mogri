using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Services;
using Polly;
using System.Net;

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
        }).AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(500), (message, timeSpan) =>
        {

        }))
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
           Proxy = new WebProxy() { Address = new Uri("http://192.168.68.52:9000") }
        });

        builder.Services.AddSingleton<IImageGenerationService, SdForgeNeoService>();
        //builder.Services.AddSingleton<IImageGenerationService, Automatic1111Service>();
        //builder.Services.AddSingleton<IStableDiffusionService, LSteinStableDiffusionService>();
        builder.Services.AddSingleton<IPopupService, PopupService>();
        builder.Services.AddSingleton<IImageService, ImageService>();
        builder.Services.AddSingleton<ISegmentationService, SegmentationService>();
        builder.Services.AddSingleton<ILoadingService, LoadingService>();

#if ANDROID
        builder.Services.AddSingleton<IFileService, FileService>();
#endif

        return builder;
    }
}
