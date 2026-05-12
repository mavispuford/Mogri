namespace Mogri.Models;

/// <summary>
/// Carries the latest segmentation-image setup state from the coordinator to the viewmodel.
/// </summary>
public sealed class CanvasSegmentationImageStateChangedEventArgs : EventArgs
{
    public CanvasSegmentationImageStateChangedEventArgs(bool isSettingImage, bool hasSegmentationImage)
    {
        IsSettingImage = isSettingImage;
        HasSegmentationImage = hasSegmentationImage;
    }

    public bool IsSettingImage { get; }

    public bool HasSegmentationImage { get; }
}