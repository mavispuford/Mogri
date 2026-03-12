using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;
using Mogri.Models;
using Mogri.Views.Popups;
using Mopups.Pages;

namespace Mogri.Registrations;

public static class PopupRegistrations
{
    private static Dictionary<string, Type> _registrations = new();

    public static MauiAppBuilder RegisterPopups(this MauiAppBuilder builder)
    {
        registerPopup<IResultItemPopupViewModel, ResultItemPopup>(builder.Services, () => new ResultItemPopup());
        registerPopup<IColorPickerPopupViewModel, ColorPickerPopup>(builder.Services, () => new ColorPickerPopup());
        registerPopup<IEditMasksPopupViewModel, EditMasksPopup>(builder.Services, () => new EditMasksPopup());
        registerPopup<IEditMaskItemPopupViewModel, EditMaskItemPopup>(builder.Services, () => new EditMaskItemPopup());
        registerPopup<IPromptStyleInfoPopupViewModel, PromptStyleInfoPopup>(builder.Services, () => new PromptStyleInfoPopup());
        registerPopup<IHistoryItemPopupViewModel, HistoryItemPopup>(builder.Services, () => new HistoryItemPopup());
        registerPopup<IResolutionSelectPopupViewModel, ResolutionSelectPopup>(builder.Services, () => new ResolutionSelectPopup());
        registerSingletonPopup<LoadingPopup>(builder.Services, () => new LoadingPopup());

        return builder;
    }

    private static void registerPopup<TPopup>(IServiceCollection serviceCollection, Func<TPopup> factory)
        where TPopup : PopupPage
    {
        _registrations[typeof(TPopup).Name] = typeof(TPopup);

        serviceCollection.AddTransient<TPopup>(provider => factory());
    }

    private static void registerPopup<TViewModel, TPopup>(IServiceCollection serviceCollection, Func<TPopup> factory)
        where TViewModel : IPopupBaseViewModel
        where TPopup : PopupPage
    {
        _registrations[typeof(TPopup).Name] = typeof(TPopup);

        serviceCollection.AddTransient<TPopup>(provider =>
        {
            var popup = factory();

            var viewModel = provider.GetService<TViewModel>()
                ?? throw new InvalidOperationException($"Unable to create a viewmodel of type {typeof(TViewModel)}");

            popup.BindingContext = viewModel;

            return popup;
        });
    }

    private static void registerSingletonPopup<TPopup>(IServiceCollection serviceCollection, Func<TPopup> factory)
        where TPopup : PopupPage
    {
        _registrations[typeof(TPopup).Name] = typeof(TPopup);

        serviceCollection.AddSingleton<TPopup>(provider => factory());
    }

    public static Type GetPopupTypeByName(string name)
    {
        return _registrations[name];
    }
}
