namespace Mogri.Enums;

/// <summary>
/// Determines how the canvas image and mask layers are combined when sending to generation.
/// </summary>
public enum CanvasUseMode
{
    Inpaint,
    PaintOnly,
    MaskOnly,
    ImageOnly
}
