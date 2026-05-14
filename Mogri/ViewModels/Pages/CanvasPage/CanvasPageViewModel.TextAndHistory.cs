using CommunityToolkit.Mvvm.Input;
using SkiaSharp;

namespace Mogri.ViewModels;

/// <summary>
/// Canvas page view model partial that owns undo, history actions, and user-facing text add, edit, delete, and duplicate flows.
/// </summary>
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
}