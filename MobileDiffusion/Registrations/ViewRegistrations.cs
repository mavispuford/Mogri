using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.ViewModels.Pages;
using MobileDiffusion.Views;

namespace MobileDiffusion.Registrations;

public static class ViewRegistrations
{
    public static MauiAppBuilder RegisterViews(this MauiAppBuilder builder)
    {
        registerPage<IMainPageViewModel, MainPage>(builder.Services, () => new MainPage());
        registerPage<IPromptSettingsPageViewModel, PromptSettingsPage>(builder.Services, () => new PromptSettingsPage());
        registerPage<IImageToImageSettingsPageViewModel, ImageToImageSettingsPage>(builder.Services, () => new ImageToImageSettingsPage());
        registerPage<ICanvasPageViewModel, CanvasPage>(builder.Services, () => new CanvasPage());
        registerPage<IAppSettingsPageViewModel, AppSettingsPage>(builder.Services, () => new AppSettingsPage());
        registerPage<IPromptPageViewModel, PromptPage>(builder.Services, () => new PromptPage());
        registerPage<IPromptStyleSelectionPageViewModel, PromptStyleSelectionPage>(builder.Services, () => new PromptStyleSelectionPage());
        registerPage<IHistoryPageViewModel, HistoryPage>(builder.Services, () => new HistoryPage());
        registerPage<ILoraSelectionPageViewModel, LoraSelectionPage>(builder.Services, () => new LoraSelectionPage());

        return builder;
    }

    private static void registerPage<TViewModel, TPage>(IServiceCollection serviceCollection, Func<TPage> factory)
        where TViewModel : IPageViewModel
        where TPage : BasePage
    {
        Routing.RegisterRoute(typeof(TPage).Name, typeof(TPage));

        serviceCollection.AddTransient<TPage>(provider =>
        {
            var page = factory();

            var viewModel = provider.GetService<TViewModel>()
                ?? throw new InvalidOperationException($"Unable to create a viewmodel of type {typeof(TViewModel)}");

            page.BindingContext = viewModel;

            return page;
        });
    }
}
