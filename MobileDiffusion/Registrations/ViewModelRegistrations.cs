using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.ViewModels;
using MobileDiffusion.Views.Popups;

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
        builder.Services.AddTransient<IHistoryPageViewModel, HistoryPageViewModel>();
        builder.Services.AddTransient<IHistoryItemViewModel, HistoryItemViewModel>();
        builder.Services.AddTransient<IHistoryItemPopupViewModel, HistoryItemPopupViewModel>();
        builder.Services.AddTransient<IResolutionSelectPopupViewModel, ResolutionSelectPopupViewModel>();
        builder.Services.AddTransient<ILoraSelectionPageViewModel, LoraSelectionPageViewModel>();
        builder.Services.AddTransient<ILoraViewModel, LoraViewModel>();
        builder.Services.AddTransient<IModelViewModel, ModelViewModel>();
        builder.Services.AddTransient<IUpscalerViewModel, UpscalerViewModel>();
        builder.Services.AddTransient<IPromptStyleViewModel, PromptStyleViewModel>();

        return builder;
    }
}
