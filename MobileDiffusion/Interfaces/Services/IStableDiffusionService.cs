using MobileDiffusion.Models;
using MobileDiffusion.Models.LStein;

namespace MobileDiffusion.Interfaces.Services;

public interface IStableDiffusionService
{
    public Task<bool> CheckServer();

    public IAsyncEnumerable<LSteinResponseItem> SubmitTextToImageRequest(Settings settings);

    Task<byte[]> GetImageBytesAsync(LSteinResponseItem responseItem);
}
