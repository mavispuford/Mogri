using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.ViewModels;

namespace MobileDiffusion;

public static class ViewModelRegistrations
{
    public static MauiAppBuilder RegisterViewModels(this MauiAppBuilder builder)
    {
        builder.Services.AddTransient<IMainPageViewModel, MainPageViewModel>();
        builder.Services.AddTransient<IPromptSettingsPageViewModel, PromptSettingsPageViewModel>();

        return builder;
    }
}
