namespace MobileDiffusion.Interfaces.Services;

/// <summary>
/// Represents a concrete image generation backend (e.g., SD Forge Neo, ComfyUI).
/// </summary>
public interface IImageGenerationBackend : IImageGenerationService
{
    /// <summary>
    /// The unique name of the backend (e.g., "SD Forge Neo").
    /// This is used for registration and selection in the settings.
    /// </summary>
    string Name { get; }
}
