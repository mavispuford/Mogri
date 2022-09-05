#nullable enable

using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Interfaces.Services
{
    public interface IPopupService
    {
        Task<object?> ShowPopupAsync(string name, IDictionary<string, object> parameters);

        void ClosePopup(IPopupBaseViewModel viewModel, object? result);
    }
}
