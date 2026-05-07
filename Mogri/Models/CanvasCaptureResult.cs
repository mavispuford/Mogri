using SkiaSharp;

namespace Mogri.Models;

public class CanvasCaptureResult
{
    public required SKBitmap MaskBitmap { get; set; }

    public SKBitmap? PreparedSourceBitmap { get; set; }
}
