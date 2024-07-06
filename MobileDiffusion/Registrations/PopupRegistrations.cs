using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using MobileDiffusion.Views.Popups;
using Mopups.Pages;

namespace MobileDiffusion.Registrations;

public static class PopupRegistrations
{
    private static Dictionary<string, Type> _registrations = new();

    public static MauiAppBuilder RegisterPopups(this MauiAppBuilder builder)
    {
        registerPopup<IResultItemPopupViewModel, ResultItemPopup>(builder.Services);
        registerPopup<IColorPickerPopupViewModel, ColorPickerPopup>(builder.Services);
        registerPopup<IPromptStyleInfoPopupViewModel, PromptStyleInfoPopup>(builder.Services);
        registerPopup<IHistoryItemPopupViewModel, HistoryItemPopup>(builder.Services);
        registerPopup<IResolutionSelectPopupViewModel, ResolutionSelectPopup>(builder.Services);
        registerSingletonPopup<LoadingPopup>(builder.Services);

        return builder;
    }

    private static void registerPopup<TPopup>(IServiceCollection serviceCollection)
    where TPopup : PopupPage
    {
        _registrations[typeof(TPopup).Name] = typeof(TPopup);

        serviceCollection.AddTransient(provider =>
        {
            var popup = Activator.CreateInstance(typeof(TPopup)) as TPopup;

            if (popup == null)
            {
                throw new InvalidOperationException($"Unable to create a popup of type {typeof(TPopup)}");
            }

            return popup;
        });
    }

    private static void registerPopup<TViewModel, TPopup>(IServiceCollection serviceCollection)
        where TViewModel : IPopupBaseViewModel
        where TPopup : PopupPage
    {
        _registrations[typeof(TPopup).Name] = typeof(TPopup);

        serviceCollection.AddTransient(provider =>
        {
            var popup = Activator.CreateInstance(typeof(TPopup)) as TPopup;

            if (popup == null)
            {
                throw new InvalidOperationException($"Unable to create a popup of type {typeof(TPopup)}");
            }

            var viewModel = provider.GetService<TViewModel>();

            if (viewModel == null)
            {
                throw new InvalidOperationException($"Unable to create a viewmodel of type {typeof(TViewModel)}");
            }

            popup.BindingContext = viewModel;

            return popup;
        });
    }

    private static void registerSingletonPopup<TPopup>(IServiceCollection serviceCollection)
    where TPopup : PopupPage
    {
        _registrations[typeof(TPopup).Name] = typeof(TPopup);

        serviceCollection.AddSingleton(provider =>
        {
            var popup = Activator.CreateInstance(typeof(TPopup)) as TPopup;

            if (popup == null)
            {
                throw new InvalidOperationException($"Unable to create a popup of type {typeof(TPopup)}");
            }

            return popup;
        });
    }

    public static Type GetPopupTypeByName(string name)
    {
        return _registrations[name];
    }
}
