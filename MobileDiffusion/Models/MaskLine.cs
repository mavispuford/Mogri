using SkiaSharp;

namespace MobileDiffusion.Models;

public class MaskLine
{
    public float Alpha { get; set; }
    public float BrushSize { get; set; }
    public Color Color { get; set; }
    public List<SKPoint> Path { get; set; } = new();
}
