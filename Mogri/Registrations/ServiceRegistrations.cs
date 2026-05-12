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
        builder.Services.AddSingleton<IImageGenerationBackend, ComfyCloudService>();
        builder.Services.AddSingleton<IBackendRegistry, BackendRegistry>();
        builder.Services.AddSingleton<IAnimationService, AnimationService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IMainThreadService, MainThreadService>();
        builder.Services.AddSingleton<IToastService, ToastService>();
        builder.Services.AddSingleton<IHapticsService, HapticsService>();
        builder.Services.AddSingleton<IPopupService, PopupService>();
        builder.Services.AddSingleton<IImageService, ImageService>();
        builder.Services.AddSingleton<ICanvasActionBitmapService, CanvasActionBitmapService>();
        builder.Services.AddSingleton<ICanvasBitmapService, CanvasBitmapService>();
        builder.Services.AddSingleton<ISegmentationService, SegmentationService>();
        builder.Services.AddSingleton<IPresetService, PresetService>();
        builder.Services.AddSingleton<IPatchService, AotGanPatchService>();
        builder.Services.AddSingleton<IHistoryService, HistoryService>();
        builder.Services.AddSingleton<IPromptStyleService, PromptStyleService>();
        builder.Services.AddSingleton<ICanvasHistoryService, CanvasHistoryService>();
        builder.Services.AddSingleton<ILicenseService, LicenseService>();
        builder.Services.AddSingleton<ICheckpointSettingsService, CheckpointSettingsService>();

#if ANDROID
        builder.Services.AddSingleton<IFileService, AndroidFileService>();
#elif IOS
        builder.Services.AddSingleton<IFileService, IosFileService>();
#endif

        return builder;
    }
}
