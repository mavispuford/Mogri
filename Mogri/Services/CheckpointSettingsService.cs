using System.Text.Json;
using Mogri.Interfaces.Services;
using Mogri.Models;

namespace Mogri.Services;

public class CheckpointSettingsService : ICheckpointSettingsService
{
    private const string PreferenceKeyPrefix = "CheckpointSettings:";

    public void Save(string checkpointKey, CheckpointSettings settings)
    {
        if (string.IsNullOrWhiteSpace(checkpointKey))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(settings);

        var key = BuildPreferenceKey(checkpointKey);
        var json = JsonSerializer.Serialize(settings);

        Preferences.Default.Set(key, json);
    }

    public CheckpointSettings? Load(string checkpointKey)
    {
        if (string.IsNullOrWhiteSpace(checkpointKey))
        {
            return null;
        }

        var key = BuildPreferenceKey(checkpointKey);
        var json = Preferences.Default.Get(key, string.Empty);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CheckpointSettings>(json);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static string BuildPreferenceKey(string checkpointKey)
    {
        return $"{PreferenceKeyPrefix}{checkpointKey}";
    }
}
