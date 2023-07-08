using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.Services;

public interface IStableDiffusionService
{
    public Task<bool> CheckServer();

    public IAsyncEnumerable<ApiResponse> SubmitImageRequest(Settings settings);

    Task<byte[]> GetImageBytesAsync(string url);

    Task Initialize();

    Task RefreshResources();

    Dictionary<string, string> Samplers { get; }

    bool Initialized { get; }
}
