using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MobileDiffusion.Messages;

public class AppLifecycleMessage : ValueChangedMessage<bool>
{
    public AppLifecycleMessage(bool isForeground) : base(isForeground)
    {
    }
}
