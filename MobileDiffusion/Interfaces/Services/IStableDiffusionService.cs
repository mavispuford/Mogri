using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.Services;

public interface IStableDiffusionService
{
    public Task<bool> CheckServerAsync();

    public IAsyncEnumerable<ApiResponse> SubmitImageRequestAsync(PromptSettings settings);

    Task<byte[]> GetImageBytesAsync(string url);

    Task<PromptSettings> GetImageInfoAsync(string base64EncodedImage);

    Task InitializeAsync();

    Task RefreshResourcesAsync();

    Task<Dictionary<string, string>> GetSamplersAsync();

    Task<List<IPromptStyleViewModel>> GetPromptStylesAsync();

    Task<Dictionary<string, string>> GetModelsAsync();

    Task<List<ILoraViewModel>> GetLorasAsync();

    bool Initialized { get; }
}
