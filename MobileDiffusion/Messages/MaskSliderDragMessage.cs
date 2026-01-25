using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MobileDiffusion.Messages;

public class MaskSliderDragMessage : ValueChangedMessage<bool>
{
    public MaskSliderDragMessage(bool isDragging) : base(isDragging)
    {
    }
}
