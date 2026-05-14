using Mogri.Interfaces.Services;

namespace Mogri.Interfaces.Coordinators;

/// <summary>
/// Coordinates backend selection and routes image-generation operations to the active backend.
/// </summary>
public interface IImageGenerationCoordinator : IImageGenerationService
{
}