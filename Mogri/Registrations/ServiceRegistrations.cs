using Mogri.Interfaces.Services;
using Mogri.Services;
using System.Net;

#if ANDROID
using Mogri.Platforms.Android.Services;
#elif IOS
using Mogri.Platforms.iOS.Services;
#endif

namespace Mogri.Registrations;

public static class ServiceRegistrations
{
    public static MauiAppBuilder RegisterServices(this MauiAppBuilder builder)
    {
        builder.Services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
#if ANDROID
            return new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(20),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(20),

                // Keep around for local HTTP capture when debugging
// #if DEBUG
//                 Proxy = new WebProxy() { Address = new Uri("http://192.168.68.52:9000") }
// #endif
            };
#else
            return new HttpClientHandler();
#endif
        });

        builder.Services.AddSingleton<IImageGenerationBackend, SdForgeNeoService>();
        builder.Services.AddSingleton<IImageGenerationBackend, ComfyUiService>();
        builder.Services.AddSingleton<IImageGenerationBackend, ComfyUiCloudService>();
        builder.Services.AddSingleton<IBackendRegistry, BackendRegistry>();
        builder.Services.AddSingleton<IImageGenerationService, ProxyImageGenerationService>();
        builder.Services.AddSingleton<IPopupService, PopupService>();
        builder.Services.AddSingleton<IImageService, ImageService>();
        builder.Services.AddSingleton<ISegmentationService, SegmentationService>();
        builder.Services.AddSingleton<ILoadingService, LoadingService>();
        builder.Services.AddSingleton<IPresetService, PresetService>();
        builder.Services.AddSingleton<IPatchService, AotGanPatchService>();
        builder.Services.AddSingleton<IHistoryService, HistoryService>();
        builder.Services.AddSingleton<ICanvasHistoryService, CanvasHistoryService>();
        builder.Services.AddSingleton<ILicenseService, LicenseService>();

#if ANDROID
        builder.Services.AddSingleton<IGenerationTaskService, AndroidGenerationTaskService>();
        builder.Services.AddSingleton<IFileService, AndroidFileService>();
#elif IOS
        builder.Services.AddSingleton<IGenerationTaskService, IosGenerationTaskService>();
        builder.Services.AddSingleton<IFileService, IosFileService>();
#else
        builder.Services.AddSingleton<IGenerationTaskService, GenerationTaskService>();
#endif

        return builder;
    }
}
