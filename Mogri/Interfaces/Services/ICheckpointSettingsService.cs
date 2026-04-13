using Mogri.Models;

namespace Mogri.Interfaces.Services;

/// <summary>
/// Persists and retrieves per-checkpoint generation settings.
/// </summary>
public interface ICheckpointSettingsService
{
    /// <summary>
    /// Saves generation settings for the specified checkpoint.
    /// </summary>
    void Save(string checkpointKey, CheckpointSettings settings);

    /// <summary>
    /// Retrieves persisted generation settings for the specified checkpoint, or null if none exist.
    /// </summary>
    CheckpointSettings? Load(string checkpointKey);
}
