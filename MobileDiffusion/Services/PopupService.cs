#nullable enable

using CommunityToolkit.Maui.Views;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.ViewModels.Popups;
using MobileDiffusion.Registrations;
using Mopups.Pages;
using Mopups.Services;
using System.Linq;

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

        public async Task ShowPopupAsync(string name, IDictionary<string, object>? parameters)
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
            else if (popup is IQueryAttributable queryAttributablePopup && parameters != null)
            {
                queryAttributablePopup.ApplyQueryAttributes(parameters);
            }

            var tcs = new TaskCompletionSource<object?>();

            activePopups.Add(popup, tcs);

            await MopupService.Instance.PushAsync(popup);

            // It takes a bit of time for the popup to show...
            for (var i = 0; i < 5; i++)
            {
                if (MopupService.Instance.PopupStack.Contains(popup))
                {
                    return;
                }

                await Task.Delay(100);
            }
        }

        public async Task<object?> ShowPopupForResultAsync(string name, IDictionary<string, object>? parameters)
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

            if (MopupService.Instance.PopupStack.Contains(popup.Key))
            {
                await MopupService.Instance.RemovePageAsync(popup.Key);
            }

            popup.Value.SetResult(result);

            activePopups.Remove(popup.Key);
        }

        public async Task ClosePopupAsync(string name, object? result)
        {
            var popupType = PopupRegistrations.GetPopupTypeByName(name);

            var popup = activePopups.Keys.FirstOrDefault(p => p.GetType().Name == popupType.Name);

            if (popup == null)
            {
                return;
            }

            if (MopupService.Instance.PopupStack.Contains(popup))
            {
                await MopupService.Instance.RemovePageAsync(popup);
            }

            activePopups[popup].SetResult(result);

            activePopups.Remove(popup);
        }

        public async Task ClosePopupAsync(object? result)
        {
            if (!activePopups.Any())
            {
                return;
            }

            var popup = activePopups.Last();

            if (MopupService.Instance.PopupStack.Contains(popup.Key))
            {
                await MopupService.Instance.RemovePageAsync(popup.Key);
            }

            popup.Value.SetResult(result);

            activePopups.Remove(popup.Key);
        }

        public Task DisplayAlertAsync(string title, string message, string cancel)
        {
            return Shell.Current.Dispatcher.DispatchAsync(() => Shell.Current.DisplayAlertAsync(title, message, cancel));
        }

        public Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel)
        {
            return Shell.Current.Dispatcher.DispatchAsync(() => Shell.Current.DisplayAlertAsync(title, message, accept, cancel));
        }

        public Task<string?> DisplayPromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel", string? placeholder = null, int maxLength = -1, Keyboard? keyboard = null, string initialValue = "")
        {
            return Shell.Current.Dispatcher.DispatchAsync(() => Shell.Current.DisplayPromptAsync(title, message, accept, cancel, placeholder, maxLength, keyboard, initialValue));
        }

        public Task<string> DisplayActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
        {
            return Shell.Current.Dispatcher.DispatchAsync(() => Shell.Current.DisplayActionSheetAsync(title, cancel, destruction, buttons));
        }
    }
}
