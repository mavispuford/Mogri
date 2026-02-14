using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.Services;

public interface IImageGenerationService
{
    public Task<bool> CheckServerAsync(CancellationToken cancellationToken = default);

    public IAsyncEnumerable<ApiResponse> SubmitImageRequestAsync(PromptSettings settings, CancellationToken cancellationToken = default);

    Task<byte[]> GetImageBytesAsync(string url, CancellationToken cancellationToken = default);

    Task<PromptSettings?> GetImageInfoAsync(string base64EncodedImage, CancellationToken cancellationToken = default);

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task RefreshResourcesAsync(CancellationToken cancellationToken = default);

    Task<Dictionary<string, string>> GetSamplersAsync(CancellationToken cancellationToken = default);

    Task<List<IPromptStyleViewModel>> GetPromptStylesAsync(CancellationToken cancellationToken = default);

    Task<List<IModelViewModel>> GetModelsAsync(CancellationToken cancellationToken = default);

    Task<List<ILoraViewModel>> GetLorasAsync(CancellationToken cancellationToken = default);

    Task<List<IUpscalerViewModel>> GetUpscalersAsync(CancellationToken cancellationToken = default);

    Task<List<string>> GetSchedulersAsync(CancellationToken cancellationToken = default);

    Task<IModelViewModel?> GetSelectedModelAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(PromptSettings settings, CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(CancellationToken cancellationToken = default);

    bool Initialized { get; }
}
