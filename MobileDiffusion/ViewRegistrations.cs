using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Views;
using System.ComponentModel;

namespace MobileDiffusion
{
    public static class ViewRegistrations
    {
        public static MauiAppBuilder RegisterViews(this MauiAppBuilder builder)
        {
            registerPage<IMainPageViewModel, MainPage>(builder.Services);

            return builder;
        }

        private static void registerPage<TViewModel, TPage>(IServiceCollection serviceCollection)
            where TViewModel : IBaseViewModel
            where TPage : Page
        {
            serviceCollection.AddTransient(provider =>
            {
                var page = Activator.CreateInstance(typeof(TPage)) as TPage;

                if (page == null)
                {
                    throw new InvalidOperationException($"Unable to create a page of type {typeof(TPage)}");
                }

                var viewModel = provider.GetService<TViewModel>();

                if (viewModel == null)
                {
                    throw new InvalidOperationException($"Unable to create a viewmodel of type {typeof(TViewModel)}");
                }

                page.BindingContext = viewModel;

                return page;
            });
        }
    }
}
