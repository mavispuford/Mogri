namespace Mogri.ViewModels;

/// <summary>
/// Canvas page view model partial that owns shared canvas state helpers such as ordering, cloning, and clear-all coordination.
/// </summary>
public partial class CanvasPageViewModel
{
    private async Task clearAllActionsAndHistoryAsync()
    {
        await _canvasHistoryService.ClearAllAsync();
        CanvasActions.Clear();
        TextElements.Clear();
    }

    private int getNextCanvasOrder()
    {
        var nextCanvasActionOrder = CanvasActions.Count == 0 ? 0 : CanvasActions.Max(canvasAction => canvasAction.Order) + 1;
        var nextTextOrder = TextElements.Count == 0 ? 0 : checked((int)(TextElements.Max(textElement => textElement.Order) + 1));

        return Math.Max(nextCanvasActionOrder, nextTextOrder);
    }

    private static TextElementViewModel cloneTextElement(TextElementViewModel textElement)
    {
        return new TextElementViewModel(textElement.Id, textElement.Order, textElement.BaseFontSize)
        {
            Text = textElement.Text,
            X = textElement.X,
            Y = textElement.Y,
            Scale = textElement.Scale,
            Rotation = textElement.Rotation,
            Color = textElement.Color,
            Alpha = textElement.Alpha,
            Noise = textElement.Noise,
            IsSelected = textElement.IsSelected
        };
    }
}