using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Maui.Core.Extensions;
using Mogri.Helpers;
using SkiaSharp;

namespace Mogri.ViewModels;

public abstract partial class PaintActionViewModel : CanvasActionViewModel
{
    // Shared shaders and paint color for paint-like canvas actions
    protected SKShader? _bitmapShader;
    protected SKShader? _noiseShader;
    protected SKColor _paintColor;

    [ObservableProperty]
    public partial float Alpha { get; set; }

    [ObservableProperty]
    public partial Color Color { get; set; } = Colors.Transparent;

    [ObservableProperty]
    public partial double Noise { get; set; }

    partial void OnColorChanged(Color value)
    {
        UpdateShaders();
    }

    partial void OnAlphaChanged(float value)
    {
        UpdateShaders();
    }

    partial void OnNoiseChanged(double value)
    {
        UpdateShaders();
    }

    protected void UpdateShaders()
    {
        if (Color == null || Alpha < 0 || Alpha > 1)
        {
            return;
        }

        _paintColor = new SKColor(
            Color.GetByteRed(),
            Color.GetByteGreen(),
            Color.GetByteBlue(),
            Convert.ToByte((int)Math.Max(1, Alpha * 255)));

        _bitmapShader = MaskHelper.CreateMaskBitmapShaderLines(_paintColor);

        if (Noise > 0)
        {
            _noiseShader = NoiseShaderHelper.CreateNoiseShader(_paintColor, Noise);
        }
        else
        {
            _noiseShader = null;
        }
    }
}
