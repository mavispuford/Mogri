namespace Mogri.Models;

/// <summary>
/// Defines which features a backend supports.
/// The UI uses this to hide/show relevant controls.
/// </summary>
public record BackendCapabilities
{
    /// <summary>
    /// Gets whether the backend supports tiled/seamless generation.
    /// </summary>
    public bool SupportsSeamless { get; init; }

    /// <summary>
    /// Gets whether the backend supports upscaling.
    /// </summary>
    public bool SupportsUpscaling { get; init; }

    /// <summary>
    /// Gets whether the backend can provide a list of samplers.
    /// </summary>
    public bool SupportsSamplerList { get; init; }

    /// <summary>
    /// Gets whether the backend supports cancelling a generation request.
    /// </summary>
    public bool SupportsCancellation { get; init; }

    /// <summary>
    /// Gets whether the backend supports LoRA (Low-Rank Adaptation) models.
    /// </summary>
    public bool SupportsLoras { get; init; }

    /// <summary>
    /// Gets whether the backend supports predefined prompt styles.
    /// </summary>
    public bool SupportsStyles { get; init; }

    /// <summary>
    /// Gets whether the backend supports explicit scheduler selection (separate from samplers).
    /// </summary>
    public bool SupportsSchedulers { get; init; }

    /// <summary>
    /// Gets whether the backend supports explicit VAE selection.
    /// </summary>
    public bool SupportsVaes { get; init; }

    /// <summary>
    /// Gets whether the backend supports explicit Text Encoder selection.
    /// </summary>
    public bool SupportsTextEncoders { get; init; }

    public static BackendCapabilities None => new();

    public static BackendCapabilities Full => new()
    {
        SupportsSeamless = true,
        SupportsUpscaling = true,
        SupportsSamplerList = true,
        SupportsCancellation = true,
        SupportsLoras = true,
        SupportsStyles = true,
        SupportsSchedulers = true,
        SupportsVaes = true,
        SupportsTextEncoders = true
    };
}
