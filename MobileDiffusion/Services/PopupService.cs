#nullable enable

using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Registrations;
using Mopups.Pages;
using Mopups.Services;

namespace MobileDiffusion.Services
{
    public class PopupService : IPopupService
    {
        private static Dictionary<PopupPage, TaskCompletionSource<object?>> activePopups = new();
        
        private readonly IServiceProvider _serviceProvider;

        public PopupService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<object?> ShowPopupAsync(string name, IDictionary<string, object> parameters)
        {
            var popupType = PopupRegistrations.GetPopupTypeByName(name);
            var popup = _serviceProvider.GetService(popupType) as PopupPage;

            if (popup == null)
            {
                throw new InvalidCastException("Unable to create the requested popup");
            }

            if (popup.BindingContext is IQueryAttributable queryAttributable &&
                parameters != null)
            {
                queryAttributable.ApplyQueryAttributes(parameters);
            }

            var tcs = new TaskCompletionSource<object?>();
            
            activePopups.Add(popup, tcs);

            await MopupService.Instance.PushAsync(popup);

            var result = await tcs.Task;

            return result;
        }

        public async Task ClosePopupAsync(IPopupBaseViewModel viewModel, object? result)
        {
            if (!activePopups.Any(p => p.Key.BindingContext == viewModel))
            {
                return;
            }

            var popup = activePopups.First(p => p.Key.BindingContext == viewModel);

            await MopupService.Instance.RemovePageAsync(popup.Key);
            
            popup.Value.SetResult(result);

            activePopups.Remove(popup.Key);
        }
    }
}