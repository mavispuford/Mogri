using Mogri.Enums;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;

namespace Mogri.Coordinators;

/// <summary>
/// Coordinates backend selection and forwards image-generation calls to the active backend.
/// </summary>
public class ImageGenerationCoordinator : IImageGenerationCoordinator
{
    private readonly IBackendRegistry _registry;
    private IImageGenerationBackend? _activeBackend;

    public ImageGenerationCoordinator(IBackendRegistry registry)
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
        var result = await ActiveBackend.GetImageInfoAsync(base64EncodedImage, cancellationToken);
        if (result != null)
        {
            return result;
        }

        return null;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _activeBackend = null;
        await ActiveBackend.InitializeAsync(cancellationToken);
    }

    public Task RefreshResourcesAsync(CancellationToken cancellationToken = default) => ActiveBackend.RefreshResourcesAsync(cancellationToken);

    public Task<Dictionary<string, string>> GetSamplersAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetSamplersAsync(cancellationToken);

    public Task<List<IModelViewModel>> GetModelsAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetModelsAsync(cancellationToken);

    public Task<List<ILoraViewModel>> GetLorasAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetLorasAsync(cancellationToken);

    public Task<List<IUpscalerViewModel>> GetUpscalersAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetUpscalersAsync(cancellationToken);

    public Task<List<string>> GetSchedulersAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetSchedulersAsync(cancellationToken);

    public Task<List<string>> GetVaesAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetVaesAsync(cancellationToken);

    public Task<List<string>> GetTextEncodersAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetTextEncodersAsync(cancellationToken);

    public Task<IModelViewModel?> GetSelectedModelAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetSelectedModelAsync(cancellationToken);

    public Task<ModelType> GetCurrentModelTypeAsync(CancellationToken cancellationToken = default) => ActiveBackend.GetCurrentModelTypeAsync(cancellationToken);

    public Task SaveSettingsAsync(PromptSettings settings, CancellationToken cancellationToken = default) => ActiveBackend.SaveSettingsAsync(settings, cancellationToken);

    public Task<bool> CancelAsync(CancellationToken cancellationToken = default) => ActiveBackend.CancelAsync(cancellationToken);
}