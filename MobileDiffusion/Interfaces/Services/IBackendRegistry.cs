namespace MobileDiffusion.Interfaces.Services;

/// <summary>
/// Manages the registration and retrieval of available image generation backends.
/// </summary>
public interface IBackendRegistry
{
    IEnumerable<IImageGenerationBackend> GetAllBackends();

    IImageGenerationBackend? GetBackend(string name);
}
