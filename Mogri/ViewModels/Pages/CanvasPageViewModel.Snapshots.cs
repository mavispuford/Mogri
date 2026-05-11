namespace Mogri.ViewModels;

/// <summary>
/// Canvas page view model partial that owns snapshot capture, snapshot markers, and collection restore plumbing.
/// </summary>
public partial class CanvasPageViewModel
{
    private int PushTextSnapshot()
    {
        var nextOrder = getNextCanvasOrder();
        CanvasActions.Add(new TextSnapshotCanvasActionViewModel
        {
            Order = nextOrder,
            TextElementsSnapshot = TextElements
                .Select(cloneTextElement)
                .ToList()
        });

        return nextOrder;
    }

    private async Task<string?> pushSnapshotAsync(string description, bool includeCanvasActions)
    {
        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null) return null;

        var actionsToSave = includeCanvasActions ? CanvasActions.ToList() : null;
        var textElementsToSave = TextElements.Count > 0
            ? TextElements.Select(cloneTextElement).ToList()
            : null;
        var snapshotId = await _canvasHistoryService.SaveSnapshotAsync(sourceBitmap, actionsToSave, textElementsToSave);

        return snapshotId;
    }

    private void insertSnapshotMarker(string snapshotId, string description, bool includeCanvasActions)
    {
        CanvasActions.Add(new SnapshotCanvasActionViewModel
        {
            Order = getNextCanvasOrder(),
            SnapshotId = snapshotId,
            Description = description,
            IncludesCanvasActions = includeCanvasActions
        });
    }

    private void restoreCanvasActions(IEnumerable<CanvasActionViewModel>? actions)
    {
        CanvasActions.Clear();

        if (actions == null)
        {
            return;
        }

        foreach (var action in actions.OrderBy(action => action.Order))
        {
            CanvasActions.Add(action);
        }
    }

    private void restoreTextElements(IEnumerable<TextElementViewModel>? textElements)
    {
        TextElements.Clear();

        if (textElements == null)
        {
            return;
        }

        foreach (var textElement in textElements.OrderBy(textElement => textElement.Order))
        {
            TextElements.Add(textElement);
        }
    }
}