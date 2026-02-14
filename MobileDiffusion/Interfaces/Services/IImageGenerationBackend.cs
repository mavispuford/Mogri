namespace MobileDiffusion.Interfaces.Services;

public interface IImageGenerationBackend : IImageGenerationService
{
    string Name { get; }
}
