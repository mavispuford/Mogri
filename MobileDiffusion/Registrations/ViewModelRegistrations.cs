using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.ViewModels;

namespace MobileDiffusion.Registrations;

public static class ViewModelRegistrations
{
    public static MauiAppBuilder RegisterViewModels(this MauiAppBuilder builder)
    {
        builder.Services.AddTransient<IColorPickerPopupViewModel, ColorPickerPopupViewModel>();
        builder.Services.AddTransient<IImageToImageSettingsPageViewModel, ImageToImageSettingsPageViewModel>();
        builder.Services.AddTransient<IMainPageViewModel, MainPageViewModel>();
        builder.Services.AddTransient<ICanvasPageViewModel, CanvasPageViewModel>();
        builder.Services.AddTransient<IPromptSettingsPageViewModel, PromptSettingsPageViewModel>();
        builder.Services.AddTransient<IResultItemPopupViewModel, ResultItemPopupViewModel>();
        builder.Services.AddTransient<IResultItemViewModel, ResultItemViewModel>();
        builder.Services.AddTransient<IAppSettingsPageViewModel, AppSettingsPageViewModel>();
        builder.Services.AddTransient<IPromptPageViewModel, PromptPageViewModel>();
        builder.Services.AddTransient<IPromptStyleSelectionPageViewModel, PromptStyleSelectionPageViewModel>();
        builder.Services.AddTransient<IPromptStyleInfoPopupViewModel, PromptStyleInfoPopupViewModel>();

        return builder;
    }
}
