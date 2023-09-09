using MobileDiffusion.Enums;
using SkiaSharp;

namespace MobileDiffusion.ViewModels;

public abstract class CanvasActionViewModel : BaseViewModel
{
    public CanvasActionType CanvasActionType { get; set; }

    public abstract void Execute(SKCanvas canvas, SKImageInfo imageInfo, bool isSaving);
}
