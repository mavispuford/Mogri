using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.Services;

public interface IStableDiffusionService
{
    public Task SubmitTextToImageRequest(TextToImageRequest request);
}
