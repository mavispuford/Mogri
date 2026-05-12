using Mogri.Coordinators;
using Mogri.Interfaces.Coordinators;

#if ANDROID
using Mogri.Platforms.Android.Coordinators;
#elif IOS
using Mogri.Platforms.iOS.Coordinators;
#endif

namespace Mogri.Registrations;

/// <summary>
/// Registers coordinator-layer types as orchestration moves out of Services and ViewModels.
/// </summary>
public static class CoordinatorRegistrations
{
    public static MauiAppBuilder RegisterCoordinators(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<ILoadingCoordinator, LoadingCoordinator>();
        builder.Services.AddSingleton<IImageGenerationCoordinator, ImageGenerationCoordinator>();
        builder.Services.AddSingleton<ICanvasWorkflowCoordinator, CanvasWorkflowCoordinator>();
        builder.Services.AddTransient<ICanvasSegmentationCoordinator, CanvasSegmentationCoordinator>();

    #if ANDROID
        builder.Services.AddSingleton<IGenerationTaskCoordinator, AndroidGenerationTaskCoordinator>();
    #elif IOS
        builder.Services.AddSingleton<IGenerationTaskCoordinator, IosGenerationTaskCoordinator>();
    #else
        builder.Services.AddSingleton<IGenerationTaskCoordinator, GenerationTaskCoordinator>();
    #endif

        return builder;
    }
}