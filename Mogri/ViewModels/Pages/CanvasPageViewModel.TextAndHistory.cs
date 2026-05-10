using CommunityToolkit.Mvvm.Input;
using Mogri.Models;
using SkiaSharp;

namespace Mogri.ViewModels;

public partial class CanvasPageViewModel
{
    [RelayCommand]
    private async Task Undo()
    {
        if (CanvasActions == null || !CanvasActions.Any())
        {
            return;
        }

        var lastAction = CanvasActions.Last();

        if (lastAction is TextSnapshotCanvasActionViewModel textSnapshot)
        {
            CanvasActions.Remove(lastAction);
            restoreTextElements(textSnapshot.TextElementsSnapshot.Select(cloneTextElement));
            return;
        }

        if (lastAction is SnapshotCanvasActionViewModel snapshot)
        {
            CanvasActions.Remove(lastAction);

            var (bitmap, actions, textElements) = await _canvasHistoryService.RestoreSnapshotAsync(snapshot.SnapshotId);

            if (bitmap != null)
            {
                PreserveZoomOnNextBitmapChange = true;
                SourceBitmap = bitmap;
            }

            restoreTextElements(textElements);

            if (snapshot.IncludesCanvasActions)
            {
                restoreCanvasActions(actions);
            }
        }
        else
        {
            CanvasActions.Remove(lastAction);
        }
    }

    private async Task clearAllActionsAndHistoryAsync()
    {
        await _canvasHistoryService.ClearAllAsync();
        CanvasActions.Clear();
        TextElements.Clear();
    }

    [RelayCommand]
    private async Task Clear()
    {
        var result = await _popupService.DisplayAlertAsync("Clear masks?", "Are you sure you would like to clear the masks?", "YES", "Cancel");

        if (!result)
        {
            return;
        }

        if (CanvasActions == null || !CanvasActions.Any())
        {
            return;
        }

        var actionsToRemove = CanvasActions.Where(a => a is not SnapshotCanvasActionViewModel).ToList();
        foreach (var a in actionsToRemove)
        {
            CanvasActions.Remove(a);
        }
    }

    [RelayCommand]
    private async Task ShowHistory()
    {
        try
        {
            var parameters = new Dictionary<string, object> {
                { "Actions", CanvasActions },
                { "TextElements", TextElements },
                { "OnSnapshotDelete", new Func<SnapshotCanvasActionViewModel, Task>(async snapshot => {
                    CanvasActions.Remove(snapshot);
                    var (bitmap, actions, textElements) = await _canvasHistoryService.RestoreSnapshotAsync(snapshot.SnapshotId);
                    if (bitmap != null)
                    {
                        PreserveZoomOnNextBitmapChange = true;
                        SourceBitmap = bitmap;
                    }
                    restoreTextElements(textElements);
                    if (snapshot.IncludesCanvasActions)
                    {
                        restoreCanvasActions(actions);
                    }
                }) },
                { "OnActionDelete", new Func<CanvasActionViewModel, Task>(action => {
                    DeleteCanvasAction(action);
                    return Task.CompletedTask;
                }) },
                { "OnActionDuplicate", new Action<CanvasActionViewModel>(action => {
                    DuplicateCanvasAction(action);
                }) },
                { "OnTextDelete", new Func<TextElementViewModel, Task>(textElement => {
                    DeleteTextCommand.Execute(textElement);
                    return Task.CompletedTask;
                }) },
                { "OnTextDuplicate", new Action<TextElementViewModel>(textElement => {
                    DuplicateTextCommand.Execute(textElement);
                }) },
                { "OnClearAll", new Func<Task>(async () => {
                    var firstSnapshot = CanvasActions.OfType<SnapshotCanvasActionViewModel>().FirstOrDefault();
                    if (firstSnapshot != null)
                    {
                        var (bitmap, _, _) = await _canvasHistoryService.RestoreSnapshotAsync(firstSnapshot.SnapshotId);
                        if (bitmap != null)
                        {
                            PreserveZoomOnNextBitmapChange = true;
                            SourceBitmap = bitmap;
                        }
                    }
                    await clearAllActionsAndHistoryAsync();
                }) }
            };

            await _popupService.ShowPopupAsync("CanvasHistoryPopup", parameters);
        }
        catch (Exception ex)
        {
            await _popupService.DisplayAlertAsync("Error", $"Unable to open history popup: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task AddText(SKPoint location)
    {
        var text = await _popupService.DisplayPromptAsync("Add Text", "Enter text or emoji:", placeholder: "Hello 👋");
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var nextOrder = PushTextSnapshot();

        TextElements.Add(new TextElementViewModel(nextOrder)
        {
            Text = text.Trim(),
            X = location.X,
            Y = location.Y,
            Color = CurrentColor,
            Alpha = (float)CurrentAlpha,
            Noise = CurrentNoise,
            Scale = 1f,
            Rotation = 0f
        });
    }

    [RelayCommand]
    private void DeleteText(TextElementViewModel element)
    {
        if (element == null || !TextElements.Contains(element))
        {
            return;
        }

        PushTextSnapshot();
        TextElements.Remove(element);
    }

    private void DeleteCanvasAction(CanvasActionViewModel action)
    {
        if (action == null
            || action is SnapshotCanvasActionViewModel
            || !CanvasActions.Contains(action))
        {
            return;
        }

        CanvasActions.Remove(action);
    }

    private void DuplicateCanvasAction(CanvasActionViewModel action)
    {
        if (action == null
            || action is SnapshotCanvasActionViewModel
            || !CanvasActions.Contains(action))
        {
            return;
        }

        var duplicatedAction = action.Clone();
        duplicatedAction.Order = getNextCanvasOrder();

        CanvasActions.Add(duplicatedAction);
    }

    [RelayCommand]
    private void DuplicateText(TextElementViewModel element)
    {
        if (element == null || !TextElements.Contains(element))
        {
            return;
        }

        var nextOrder = PushTextSnapshot();

        TextElements.Add(new TextElementViewModel(Guid.NewGuid().ToString(), nextOrder, element.BaseFontSize)
        {
            Text = element.Text,
            X = element.X,
            Y = element.Y,
            Scale = element.Scale,
            Rotation = element.Rotation,
            Color = element.Color,
            Alpha = element.Alpha,
            Noise = element.Noise
        });
    }

    [RelayCommand]
    private async Task EditText(TextElementViewModel element)
    {
        if (element == null || !TextElements.Contains(element))
        {
            return;
        }

        var updatedText = await _popupService.DisplayPromptAsync(
            "Edit Text",
            "Update text or emoji:",
            placeholder: "Hello 👋",
            initialValue: element.Text);

        if (string.IsNullOrWhiteSpace(updatedText))
        {
            return;
        }

        element.Text = updatedText.Trim();
    }

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

    private int getNextCanvasOrder()
    {
        var nextCanvasActionOrder = CanvasActions.Count == 0 ? 0 : CanvasActions.Max(canvasAction => canvasAction.Order) + 1;
        var nextTextOrder = TextElements.Count == 0 ? 0 : checked((int)(TextElements.Max(textElement => textElement.Order) + 1));

        return Math.Max(nextCanvasActionOrder, nextTextOrder);
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