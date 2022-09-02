using MobileDiffusion.Models;
using MobileDiffusion.Models.LStein;
using System.Collections.Generic;

namespace MobileDiffusion.Interfaces.Services;

public interface IStableDiffusionService
{
    public Task<bool> CheckServer();

    public IAsyncEnumerable<LSteinResponseItem> SubmitTextToImageRequest(BaseRequest request);

    Task<byte[]> GetImageBytesAsync(LSteinResponseItem responseItem);
}
