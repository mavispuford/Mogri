using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Mogri.Messages;

public class MaskSliderDragMessage : ValueChangedMessage<bool>
{
    public MaskSliderDragMessage(bool isDragging) : base(isDragging)
    {
    }
}
