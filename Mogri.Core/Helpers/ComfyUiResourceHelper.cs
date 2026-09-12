using System.Text.Json;

namespace Mogri.Helpers;

/// <summary>
/// Parses resource metadata returned by ComfyUI.
/// </summary>
public static class ComfyUiResourceHelper
{
    /// <summary>
    /// Extracts the available upscaler model names from ComfyUI object info.
    /// </summary>
    public static IReadOnlyList<string> ParseUpscalerModelNames(string? objectInfoJson)
    {
        if (string.IsNullOrWhiteSpace(objectInfoJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var document = JsonDocument.Parse(objectInfoJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("UpscaleModelLoader", out var loader) ||
                loader.ValueKind != JsonValueKind.Object ||
                !loader.TryGetProperty("input", out var input) ||
                input.ValueKind != JsonValueKind.Object ||
                !input.TryGetProperty("required", out var required) ||
                required.ValueKind != JsonValueKind.Object ||
                !required.TryGetProperty("model_name", out var modelName) ||
                modelName.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var names = new List<string>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var usesComboSchema = modelName.GetArrayLength() > 0 &&
                modelName[0].ValueKind == JsonValueKind.String &&
                string.Equals(modelName[0].GetString(), "COMBO", StringComparison.OrdinalIgnoreCase);

            if (usesComboSchema)
            {
                if (modelName.GetArrayLength() >= 2 &&
                    modelName[1].ValueKind == JsonValueKind.Object &&
                    modelName[1].TryGetProperty("options", out var options) &&
                    options.ValueKind == JsonValueKind.Array)
                {
                    addNames(options, names, seenNames);
                }

                return names;
            }

            if (modelName.EnumerateArray().All(value => value.ValueKind == JsonValueKind.String))
            {
                addNames(modelName, names, seenNames);
            }

            return names;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Returns the exact server-provided upscaler name for a case-insensitive selection.
    /// </summary>
    public static string? FindUpscalerModelName(IEnumerable<string>? availableNames, string? requestedName)
    {
        if (availableNames == null || string.IsNullOrWhiteSpace(requestedName))
        {
            return null;
        }

        var requested = requestedName.Trim();
        return availableNames.FirstOrDefault(name =>
            string.Equals(name, requested, StringComparison.OrdinalIgnoreCase));
    }

    private static void addNames(JsonElement values, ICollection<string> names, ISet<string> seenNames)
    {
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var name = value.GetString();
            if (!string.IsNullOrWhiteSpace(name) && seenNames.Add(name))
            {
                names.Add(name);
            }
        }
    }
}