#nullable enable

using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Interfaces.Services;

public interface IPopupService
{
    Task<object?> ShowPopupAsync(string name, IDictionary<string, object> parameters);

    Task ClosePopupAsync(IPopupBaseViewModel viewModel, object? result);
}
