namespace Mogri.Helpers;

/// <summary>
/// Provides display-name fallbacks for model identities returned by backends.
/// </summary>
public static class ModelIdentityHelper
{
    /// <summary>
    /// Returns the backend-provided model name, or the model key when the name is missing.
    /// </summary>
    public static string GetDisplayName(string? modelName, string? modelKey)
    {
        return !string.IsNullOrWhiteSpace(modelName)
            ? modelName
            : modelKey ?? string.Empty;
    }

    /// <summary>
    /// Determines whether two model identities refer to the same backend model.
    /// </summary>
    public static bool AreEquivalent(
        string? firstDisplayName,
        string? firstKey,
        string? secondDisplayName,
        string? secondKey)
    {
        return (!string.IsNullOrWhiteSpace(firstKey) &&
                !string.IsNullOrWhiteSpace(secondKey) &&
                string.Equals(firstKey, secondKey, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(firstDisplayName) &&
             !string.IsNullOrWhiteSpace(secondDisplayName) &&
             string.Equals(firstDisplayName, secondDisplayName, StringComparison.OrdinalIgnoreCase));
    }
}
