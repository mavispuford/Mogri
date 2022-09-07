using CommunityToolkit.Maui.Views;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using MobileDiffusion.Views.Popups;

namespace MobileDiffusion;

public static class PopupRegistrations
{
    private static Dictionary<string, Type> _registrations = new();

    public static MauiAppBuilder RegisterPopups(this MauiAppBuilder builder)
    {
        builder.Services.AddTransient<PopupSizeConstants>();

        registerPopup<IImageToImageSettingsPopupViewModel, ImageToImageSettingsPopup>(builder.Services);
        registerPopup<IPromptSettingsPopupViewModel, PromptSettingsPopup>(builder.Services);
        registerPopup<IResultItemPopupViewModel, ResultItemPopup>(builder.Services);

        return builder;
    }

    private static void registerPopup<TPopup>(IServiceCollection serviceCollection)
    where TPopup : Popup
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
        where TPopup : Popup
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

    public static Type GetPopupTypeByName(string name)
    {
        return _registrations[name];
    }
}
