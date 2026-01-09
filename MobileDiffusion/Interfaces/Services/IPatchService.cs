using SkiaSharp;

namespace MobileDiffusion.Interfaces.Services
{
    public interface IPatchService
    {
        Task<SKBitmap> PatchImageAsync(SKBitmap image, SKBitmap mask);
        void UnloadModel();
    }
}
