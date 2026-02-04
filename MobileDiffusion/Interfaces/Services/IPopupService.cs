#nullable enable

using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.ViewModels.Popups;

namespace MobileDiffusion.Interfaces.Services;

public interface IPopupService
{
    Task ShowPopupAsync(string name, IDictionary<string, object>? parameters);

    Task<object?> ShowPopupForResultAsync(string name, IDictionary<string, object>? parameters);

    Task ClosePopupAsync(IPopupBaseViewModel viewModel, object? result);

    Task ClosePopupAsync(string name, object? result);

    Task ClosePopupAsync(object? result);

    Task DisplayAlertAsync(string title, string message, string cancel);

    Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel);

    Task<string?> DisplayPromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel", string? placeholder = null, int maxLength = -1, Keyboard? keyboard = null, string initialValue = "");

    Task<string> DisplayActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons);
}
