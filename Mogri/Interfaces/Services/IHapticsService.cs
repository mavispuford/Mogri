using Mogri.Enums;

namespace Mogri.Interfaces.Services;

/// <summary>
/// Wraps the haptic feedback patterns used by the app.
/// </summary>
public interface IHapticsService
{
    bool IsSupported { get; }

    void Perform(HapticType type);
}