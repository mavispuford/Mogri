using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.Services;

public interface IStableDiffusionService
{
    public Task<bool> CheckServerAsync();

    public IAsyncEnumerable<ApiResponse> SubmitImageRequestAsync(Settings settings);

    Task<byte[]> GetImageBytesAsync(string url);

    Task InitializeAsync();

    Task RefreshResourcesAsync();

    Task<Dictionary<string, string>> GetSamplersAsync();

    Task<List<IPromptStyleViewModel>> GetPromptStylesAsync();

    bool Initialized { get; }
}
