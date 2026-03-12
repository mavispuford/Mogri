using SkiaSharp;

namespace Mogri.Interfaces.Services
{
    public interface IPatchService
    {
        Task<SKBitmap> PatchImageAsync(SKBitmap image, SKBitmap mask);
        void UnloadModel();
    }
}
