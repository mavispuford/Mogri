using SkiaSharp;

namespace Mogri.Interfaces.Services;

/// <summary>
/// Builds workflow and patch bitmaps from canvas action render inputs.
/// </summary>
public interface ICanvasActionBitmapService
{
    SKBitmap CreateRenderedLayer(IEnumerable<ICanvasRenderAction>? actions, int width, int height);

    SKBitmap CreatePatchMask(IEnumerable<ICanvasRenderAction>? actions, int width, int height, bool useLastOnly);
}