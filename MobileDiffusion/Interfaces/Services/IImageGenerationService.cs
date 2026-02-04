using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.Services;

public interface IImageGenerationService
{
    public Task<bool> CheckServerAsync();

    public IAsyncEnumerable<ApiResponse> SubmitImageRequestAsync(PromptSettings settings);

    Task<byte[]> GetImageBytesAsync(string url);

    Task<PromptSettings?> GetImageInfoAsync(string base64EncodedImage);

    Task InitializeAsync();

    Task RefreshResourcesAsync();

    Task<Dictionary<string, string>> GetSamplersAsync();

    Task<List<IPromptStyleViewModel>> GetPromptStylesAsync();

    Task<List<IModelViewModel>> GetModelsAsync();

    Task<List<ILoraViewModel>> GetLorasAsync();

    Task<List<IUpscalerViewModel>> GetUpscalersAsync();

    Task<List<string>> GetSchedulersAsync();

    Task<IModelViewModel?> GetSelectedModelAsync();
    
    Task SaveSettingsAsync(PromptSettings settings);

    Task<bool> CancelAsync();

    bool Initialized { get; }
}
