using SkiaSharp;

namespace MobileDiffusion.Models;

public class MaskLine
{
    public Color Color { get; set; }
    public List<SKPoint> Path { get; set; } = new();
}
