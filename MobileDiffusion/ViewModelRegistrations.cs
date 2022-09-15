using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.ViewModels;

namespace MobileDiffusion;

public static class ViewModelRegistrations
{
    public static MauiAppBuilder RegisterViewModels(this MauiAppBuilder builder)
    {
        builder.Services.AddTransient<IMainPageViewModel, MainPageViewModel>();
        builder.Services.AddTransient<IImageToImageSettingsPageViewModel, ImageToImageSettingsPageViewModel>();
        builder.Services.AddTransient<IPromptSettingsPageViewModel, PromptSettingsPageViewModel>();
        builder.Services.AddTransient<IResultItemViewModel, ResultItemViewModel>();
        builder.Services.AddTransient<IResultItemPopupViewModel, ResultItemPopupViewModel>();
        builder.Services.AddTransient<IMaskDrawingPageViewModel, MaskDrawingPageViewModel>();

        return builder;
    }
}
