using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.Services;

public interface IStableDiffusionService
{
    public Task<bool> CheckServer();

    public IAsyncEnumerable<ApiResponse> SubmitTextToImageRequest(Settings settings);

    Task<byte[]> GetImageBytesAsync(string url);

    Task RefreshResources();
}
