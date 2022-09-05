#nullable enable

using CommunityToolkit.Maui.Views;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Services
{
    public class PopupService : IPopupService
    {
        private static List<Popup> activePopups = new();
        
        private readonly IServiceProvider _serviceProvider;

        public PopupService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public Task<object?> ShowPopupAsync(string name, IDictionary<string, object> parameters)
        {
            var popupType = PopupRegistrations.GetPopupTypeByName(name);
            var popup = _serviceProvider.GetService(popupType) as Popup;

            if (popup == null)
            {
                throw new InvalidCastException("Unable to create the requested popup");
            }

            if (popup.BindingContext is IQueryAttributable queryAttributable &&
                parameters != null)
            {
                queryAttributable.ApplyQueryAttributes(parameters);
            }

            activePopups.Add(popup);

            return Shell.Current.CurrentPage.ShowPopupAsync(popup);
        }

        public void ClosePopup(IPopupBaseViewModel viewModel, object? result)
        {
            var popup = activePopups.FirstOrDefault(p => p.BindingContext == viewModel);

            if (popup == null)
            {
                return;
            }

            popup.Close(result);

            activePopups.Remove(popup);
        }
    }
}