using Mogri.Enums;
using SkiaSharp;

namespace Mogri.ViewModels;

/// <summary>
/// Stores a deep copy of the current text element state for undo operations.
/// </summary>
public class TextSnapshotCanvasActionViewModel : CanvasActionViewModel
{
    public List<TextElementViewModel> TextElementsSnapshot { get; set; } = new();

    public TextSnapshotCanvasActionViewModel()
    {
        CanvasActionType = CanvasActionType.TextSnapshot;
    }

    public override void Execute(SKCanvas canvas, SKImageInfo imageInfo, bool isSaving)
    {
        // No-op
    }

    public override CanvasActionViewModel Clone()
    {
        return new TextSnapshotCanvasActionViewModel
        {
            CanvasActionType = CanvasActionType,
            Order = Order,
            TextElementsSnapshot = TextElementsSnapshot
                .Select(textElement => new TextElementViewModel(textElement.Id, textElement.Order)
                {
                    Text = textElement.Text,
                    X = textElement.X,
                    Y = textElement.Y,
                    Scale = textElement.Scale,
                    Rotation = textElement.Rotation,
                    Color = textElement.Color,
                    Alpha = textElement.Alpha,
                    IsSelected = textElement.IsSelected
                })
                .ToList()
        };
    }
}