using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.Services;

public interface IStableDiffusionService
{
    public Task<bool> CheckServer();

    public Task<IEnumerable<string>> SubmitTextToImageRequest(TextToImageRequest request);
}
