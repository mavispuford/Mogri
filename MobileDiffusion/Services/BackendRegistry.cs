using MobileDiffusion.Interfaces.Services;

namespace MobileDiffusion.Services;

public class BackendRegistry : IBackendRegistry
{
    private readonly IEnumerable<IImageGenerationBackend> _backends;

    public BackendRegistry(IEnumerable<IImageGenerationBackend> backends)
    {
        _backends = backends;
    }

    public IEnumerable<IImageGenerationBackend> GetAllBackends()
    {
        return _backends;
    }

    public IImageGenerationBackend? GetBackend(string name)
    {
        return _backends.FirstOrDefault(b => b.Name == name);
    }
}
