using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.Services;

/// <summary>
/// A proxy service that sits between the ViewModels and the concrete ImageGenerationBackends.
/// It routes requests to the currently selected backend (e.g. Forge, ComfyUI) based on user settings.
/// </summary>
public class ProxyImageGenerationService : IImageGenerationService
{
    private readonly IBackendRegistry _registry;
    private IImageGenerationBackend? _activeBackend;

    public ProxyImageGenerationService(IBackendRegistry registry)
    {
        _registry = registry;
    }

    private IImageGenerationBackend ActiveBackend
    {
        get
        {
            if (_activeBackend == null)
            {
                var backendName = Preferences.Default.Get(Constants.PreferenceKeys.SelectedBackend, "SD Forge Neo");
                _activeBackend = _registry.GetBackend(backendName) ?? _registry.GetAllBackends().FirstOrDefault();
            }

            if (_activeBackend == null)
            {
                throw new InvalidOperationException("No image generation backends are registered.");
            }

            return _activeBackend;
        }
    }

    public bool Initialized => ActiveBackend.Initialized;

    public BackendCapabilities Capabilities => _activeBackend?.Capabilities ?? BackendCapabilities.None;

    public Task<bool> CheckServerAsync(CancellationToken cancellationToken = default) => ActiveBackend.CheckServerAsync(cancellationToken);

    public IAsyncEnumerable<ApiResponse> SubmitImageRequestAsync(PromptSettings settings, CancellationToken cancellationToken = default) => ActiveBackend.SubmitImageRequestAsync(settings, cancellationToken);

    public Task<byte[]> GetImageBytesAsync(string url, CancellationToken cancellationToken = default) => ActiveBackend.GetImageBytesAsync(url, cancellationToken);

    public async Task<PromptSettings?> GetImageInfoAsync(string base64EncodedImage, CancellationToken cancellationToken = default)
    {
        // Try the active backend first
        var result = await ActiveBackend.GetImageInfoAsync(base64EncodedImage, cancellationToken);
        if (result != null) return result;

        // If that fails, try all other registered backends to see if any can parse the format
        foreach (var backend in _registry.GetAllBackends())
        {
            if (backend == ActiveBackend) continue;

            try 
            {
                var backendResult = await backend.GetImageInfoAsync(base64EncodedImage, cancellationToken);
                if (backendResult != null) return backendResult;
            }
            catch
            {
                // Ignore errors from other backends
            }
        }

        return null;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Reset active backend to ensure we pick up changes in preferences
        _activeBackend = null;
        await ActiveBackend.InitializeAsync(cancellationToken);
    }

    public Task RefreshResourcesAsync(CancellationToken cancellationToken = default) => ActiveBackend.RefreshResourcesAsync(cancellationToken);

    public Task<Dictionary<string, string>> GetSamplersAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetSamplersAsync(cancellationToken);

    public Task<List<IPromptStyleViewModel>> GetPromptStylesAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetPromptStylesAsync(cancellationToken);

    public Task<List<IModelViewModel>> GetModelsAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetModelsAsync(cancellationToken);

    public Task<List<ILoraViewModel>> GetLorasAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetLorasAsync(cancellationToken);

    public Task<List<IUpscalerViewModel>> GetUpscalersAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetUpscalersAsync(cancellationToken);

    public Task<List<string>> GetSchedulersAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetSchedulersAsync(cancellationToken);

    public Task<IModelViewModel?> GetSelectedModelAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetSelectedModelAsync(cancellationToken);

    public Task SaveSettingsAsync(PromptSettings settings, CancellationToken cancellationToken = default) => ActiveBackend.SaveSettingsAsync(settings, cancellationToken);

    public Task<bool> CancelAsync(CancellationToken cancellationToken = default) => ActiveBackend.CancelAsync(cancellationToken);
}
