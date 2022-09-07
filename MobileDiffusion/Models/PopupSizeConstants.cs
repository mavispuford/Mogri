namespace MobileDiffusion.Models;

public class PopupSizeConstants
{
    public PopupSizeConstants(IDeviceDisplay deviceDisplay)
    {
        Tiny = new(0.3 * (deviceDisplay.MainDisplayInfo.Width / deviceDisplay.MainDisplayInfo.Density), 0.3 * (deviceDisplay.MainDisplayInfo.Height / deviceDisplay.MainDisplayInfo.Density));
        Small = new(0.5 * (deviceDisplay.MainDisplayInfo.Width / deviceDisplay.MainDisplayInfo.Density), 0.5 * (deviceDisplay.MainDisplayInfo.Height / deviceDisplay.MainDisplayInfo.Density));
        Medium = new(0.7 * (deviceDisplay.MainDisplayInfo.Width / deviceDisplay.MainDisplayInfo.Density), 0.7 * (deviceDisplay.MainDisplayInfo.Height / deviceDisplay.MainDisplayInfo.Density));
        Large = new(0.8 * (deviceDisplay.MainDisplayInfo.Width / deviceDisplay.MainDisplayInfo.Density), 0.8 * (deviceDisplay.MainDisplayInfo.Height / deviceDisplay.MainDisplayInfo.Density));
        ExtraLarge = new(0.9 * (deviceDisplay.MainDisplayInfo.Width / deviceDisplay.MainDisplayInfo.Density), 0.9 * (deviceDisplay.MainDisplayInfo.Height / deviceDisplay.MainDisplayInfo.Density));
        Full = new(deviceDisplay.MainDisplayInfo.Width / deviceDisplay.MainDisplayInfo.Density, deviceDisplay.MainDisplayInfo.Height / deviceDisplay.MainDisplayInfo.Density);
    }

    public Size Tiny { get; }

    public Size Small { get; }

    public Size Medium { get; }

    public Size Large { get; }

    public Size ExtraLarge { get; }

    public Size Full { get; }
}
