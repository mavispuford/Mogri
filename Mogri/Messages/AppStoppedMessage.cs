using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Mogri.Messages;

/// <summary>
/// Sent when the app window is stopped (fully backgrounded).
/// This is the last reliable callback before the OS may kill the process.
/// </summary>
public class AppStoppedMessage : ValueChangedMessage<bool>
{
    public AppStoppedMessage() : base(true)
    {
    }
}
