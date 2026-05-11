using Mogri.Interfaces.Services;
using Mogri.Enums;
using SkiaSharp;

namespace Mogri.ViewModels;

public abstract class CanvasActionViewModel : BaseViewModel, ICanvasRenderAction
{
    public CanvasActionType CanvasActionType { get; set; }

    public int Order { get; set; }

    public abstract void Execute(SKCanvas canvas, SKImageInfo imageInfo, bool isSaving);

    public abstract CanvasActionViewModel Clone();
}
