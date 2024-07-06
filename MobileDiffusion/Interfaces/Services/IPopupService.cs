#nullable enable

using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Interfaces.Services;

public interface IPopupService
{
    Task ShowPopupAsync(string name, IDictionary<string, object> parameters);

    Task<object?> ShowPopupForResultAsync(string name, IDictionary<string, object> parameters);

    Task ClosePopupAsync(IPopupBaseViewModel viewModel, object? result);

    Task ClosePopupAsync(string name, object? result);

    Task ClosePopupAsync(object? result);
}
