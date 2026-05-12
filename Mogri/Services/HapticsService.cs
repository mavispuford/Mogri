using Microsoft.Maui.Devices;
using Mogri.Enums;
using Mogri.Interfaces.Services;

namespace Mogri.Services;

/// <summary>
/// Adapter around MAUI haptic feedback APIs.
/// </summary>
public class HapticsService : IHapticsService
{
    public bool IsSupported => HapticFeedback.Default.IsSupported;

    public void Perform(HapticType type)
    {
        if (!IsSupported)
        {
            return;
        }

        HapticFeedback.Default.Perform(type switch
        {
            HapticType.Click => HapticFeedbackType.Click,
            HapticType.LongPress => HapticFeedbackType.LongPress,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        });
    }
}