namespace Mogri.Helpers;

/// <summary>
/// Matches user-facing model resource selections to backend-provided resource names.
/// </summary>
public static class ModelResourceHelper
{
    /// <summary>
    /// Finds the best available resource match for a saved or profile-provided name.
    /// </summary>
    public static string? FindMatch(IEnumerable<string>? resources, string? requestedResource)
    {
        if (resources == null || string.IsNullOrWhiteSpace(requestedResource))
        {
            return null;
        }

        var availableResources = resources
            .Where(resource => !string.IsNullOrWhiteSpace(resource))
            .ToList();
        var requested = requestedResource.Trim();

        return availableResources.FirstOrDefault(resource =>
                   string.Equals(resource, requested, StringComparison.OrdinalIgnoreCase))
            ?? availableResources.FirstOrDefault(resource =>
                   resource.Contains(requested, StringComparison.OrdinalIgnoreCase))
            ?? availableResources.FirstOrDefault(resource =>
                   Normalize(resource).Contains(Normalize(requested), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines whether a resource is the supported Krea Qwen3-VL 4B text encoder.
    /// </summary>
    public static bool IsKreaTextEncoder(string? resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return false;
        }

        var normalizedResource = Normalize(resource);
        return normalizedResource.Contains("qwen3vl4b", StringComparison.OrdinalIgnoreCase) &&
               !normalizedResource.Contains("vision", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a resource is the Qwen3 4B encoder used by Z-Image.
    /// </summary>
    public static bool IsZImageTextEncoder(string? resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return false;
        }

        var normalizedResource = Normalize(resource);
        return normalizedResource.Contains("qwen34b", StringComparison.OrdinalIgnoreCase) &&
               !normalizedResource.Contains("vision", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a resource is a Flux T5 text encoder.
    /// </summary>
    public static bool IsFluxT5TextEncoder(string? resource)
    {
        return !string.IsNullOrWhiteSpace(resource) &&
            Normalize(resource).Contains("t5", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a resource is a Flux CLIP-L text encoder.
    /// </summary>
    public static bool IsFluxClipLTextEncoder(string? resource)
    {
        return !string.IsNullOrWhiteSpace(resource) &&
            Normalize(resource).Contains("clipl", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    }
}