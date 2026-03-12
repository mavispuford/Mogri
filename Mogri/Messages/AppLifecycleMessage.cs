using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Mogri.Messages;

public class AppLifecycleMessage : ValueChangedMessage<bool>
{
    public AppLifecycleMessage(bool isForeground) : base(isForeground)
    {
    }
}
