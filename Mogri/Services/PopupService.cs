#nullable enable

using CommunityToolkit.Maui.Views;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;
using Mogri.Registrations;
using Mogri.Views.Popups;
using Mopups.Pages;
using Mopups.Services;
using System.Linq;

namespace Mogri.Services
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

        private Page GetActivePage()
        {
            return MopupService.Instance.PopupStack.LastOrDefault(p => p is not LoadingPopup) as Page 
                   ?? Shell.Current?.CurrentPage 
                   ?? Application.Current?.Windows.FirstOrDefault()?.Page!;
        }

        /// <summary>
        /// Temporarily removes the LoadingPopup (if shown) so that native iOS dialogs
        /// are not hidden behind it, then restores it after the action completes.
        /// </summary>
        private async Task<T> withLoadingPopupHiddenAsync<T>(Func<Task<T>> action)
        {
            var loadingPopup = MopupService.Instance.PopupStack
                .FirstOrDefault(p => p is LoadingPopup) as LoadingPopup;

            if (loadingPopup != null)
                await MopupService.Instance.RemovePageAsync(loadingPopup);

            try
            {
                return await action();
            }
            finally
            {
                if (loadingPopup != null)
                    await MopupService.Instance.PushAsync(loadingPopup);
            }
        }

        public async Task DisplayAlertAsync(string title, string message, string cancel)
        {
            await withLoadingPopupHiddenAsync(async () =>
            {
                await Shell.Current.Dispatcher.DispatchAsync(() => GetActivePage().DisplayAlertAsync(title, message, cancel));
                return (object?)null;
            });
        }

        public Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel)
        {
            return withLoadingPopupHiddenAsync(() =>
                Shell.Current.Dispatcher.DispatchAsync(() => GetActivePage().DisplayAlertAsync(title, message, accept, cancel)));
        }

        public Task<string?> DisplayPromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel", string? placeholder = null, int maxLength = -1, Keyboard? keyboard = null, string initialValue = "")
        {
            return withLoadingPopupHiddenAsync(() =>
                Shell.Current.Dispatcher.DispatchAsync(() => GetActivePage().DisplayPromptAsync(title, message, accept, cancel, placeholder, maxLength, keyboard, initialValue)));
        }

        public Task<string> DisplayActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
        {
            return withLoadingPopupHiddenAsync(() =>
                Shell.Current.Dispatcher.DispatchAsync(() => GetActivePage().DisplayActionSheetAsync(title, cancel, destruction, buttons)));
        }

        public async Task<FileResult?> PickSinglePhotoAsync()
        {
            var fileResult = await Shell.Current.Dispatcher.DispatchAsync(() => MediaPicker.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }));

            var photo = fileResult?.FirstOrDefault();

#if IOS
            // Poll the view controller hierarchy until the photo picker has fully
            // dismissed. A fixed delay is unreliable due to variable animation timing.
            await waitForPresentedViewControllerDismissalAsync();
#endif

            return photo;
        }

#if IOS
        /// <summary>
        /// Polls until the key window's root view controller has no presented view controller,
        /// indicating that the iOS photo picker has fully dismissed.
        /// </summary>
        private static async Task waitForPresentedViewControllerDismissalAsync(int timeoutMs = 3000)
        {
            const int interval = 50;
            var elapsed = 0;

            while (elapsed < timeoutMs)
            {
                var dismissed = await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var rootVc = getKeyWindowRootViewController();
                    return rootVc?.PresentedViewController == null;
                });

                if (dismissed)
                    return;

                await Task.Delay(interval);
                elapsed += interval;
            }
        }

        private static UIKit.UIViewController? getKeyWindowRootViewController()
        {
            foreach (var scene in UIKit.UIApplication.SharedApplication.ConnectedScenes)
            {
                if (scene is UIKit.UIWindowScene windowScene)
                {
                    foreach (var window in windowScene.Windows)
                    {
                        if (window.IsKeyWindow)
                            return window.RootViewController;
                    }
                }
            }

            return null;
        }
#endif
    }
}
