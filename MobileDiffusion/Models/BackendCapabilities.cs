namespace MobileDiffusion.Models;

public record BackendCapabilities
{
    public bool SupportsSeamless { get; init; }
    public bool SupportsFaceRestoration { get; init; }
    public bool SupportsUpscaling { get; init; }
    public bool SupportsSamplerList { get; init; }
    public bool SupportsCancellation { get; init; }
    public bool SupportsLoras { get; init; }
    public bool SupportsStyles { get; init; }
    public bool SupportsSchedulers { get; init; }

    public static BackendCapabilities None => new();

    public static BackendCapabilities Full => new()
    {
        SupportsSeamless = true,
        SupportsFaceRestoration = true,
        SupportsUpscaling = true,
        SupportsSamplerList = true,
        SupportsCancellation = true,
        SupportsLoras = true,
        SupportsStyles = true,
        SupportsSchedulers = true
    };
}
