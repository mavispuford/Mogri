namespace MobileDiffusion.Interfaces.Services;

public interface IBackendRegistry
{
    IEnumerable<IImageGenerationBackend> GetAllBackends();

    IImageGenerationBackend? GetBackend(string name);
}
