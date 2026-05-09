using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Mogri.Enums;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;
using Mogri.Messages;

namespace Mogri.ViewModels;

/// <summary>
/// ViewModel for the canvas history popup, displaying all canvas actions
/// including mask strokes and snapshot checkpoints.
/// </summary>
public partial class CanvasHistoryPopupViewModel : PopupBaseViewModel, ICanvasHistoryPopupViewModel, IRecipient<MaskSliderDragMessage>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICanvasHistoryService _canvasHistoryService;
    private ObservableCollection<CanvasActionViewModel>? _sourceActions;
    private ObservableCollection<TextElementViewModel>? _sourceTextElements;
    private Func<SnapshotCanvasActionViewModel, Task>? _onSnapshotDeleteCallback;
    private Func<CanvasActionViewModel, Task>? _onActionDeleteCallback;
    private Action<CanvasActionViewModel>? _onActionDuplicateCallback;
    private Func<TextElementViewModel, Task>? _onTextDeleteCallback;
    private Action<TextElementViewModel>? _onTextDuplicateCallback;
    private Func<Task>? _onClearAllCallback;

    [ObservableProperty]
    private ObservableCollection<ICanvasHistoryItemViewModel> _items = new();

    public CanvasHistoryPopupViewModel(IPopupService popupService, IServiceProvider serviceProvider, ICanvasHistoryService canvasHistoryService) : base(popupService)
    {
        _serviceProvider = serviceProvider;
        _canvasHistoryService = canvasHistoryService;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public void Receive(MaskSliderDragMessage message)
    {
        ContentOpacity = message.Value ? 0 : 1;

        if (message.Value)
        {
            PopupBackgroundColor = Colors.Transparent;
        }
        else
        {
            if (Application.Current != null && Application.Current.Resources.TryGetValue("BlackSeventyThreePercent", out var bgColor))
            {
                PopupBackgroundColor = (Color)bgColor;
            }
            else
            {
                PopupBackgroundColor = Color.FromArgb("BB000000");
            }
        }
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue("Actions", out var actionsObj) && actionsObj is ObservableCollection<CanvasActionViewModel> actions)
        {
            _sourceActions = actions;
        }

        if (query.TryGetValue("TextElements", out var textElementsObj) && textElementsObj is ObservableCollection<TextElementViewModel> textElements)
        {
            _sourceTextElements = textElements;
        }
        
        if (query.TryGetValue("OnSnapshotDelete", out var callbackObj) && callbackObj is Func<SnapshotCanvasActionViewModel, Task> callback)
        {
            _onSnapshotDeleteCallback = callback;
        }

        if (query.TryGetValue("OnActionDelete", out var actionDeleteObj) && actionDeleteObj is Func<CanvasActionViewModel, Task> actionDeleteCallback)
        {
            _onActionDeleteCallback = actionDeleteCallback;
        }

        if (query.TryGetValue("OnActionDuplicate", out var actionDuplicateObj) && actionDuplicateObj is Action<CanvasActionViewModel> actionDuplicateCallback)
        {
            _onActionDuplicateCallback = actionDuplicateCallback;
        }

        if (query.TryGetValue("OnTextDelete", out var textDeleteObj) && textDeleteObj is Func<TextElementViewModel, Task> textDeleteCallback)
        {
            _onTextDeleteCallback = textDeleteCallback;
        }

        if (query.TryGetValue("OnTextDuplicate", out var textDuplicateObj) && textDuplicateObj is Action<TextElementViewModel> textDuplicateCallback)
        {
            _onTextDuplicateCallback = textDuplicateCallback;
        }

        if (query.TryGetValue("OnClearAll", out var clearAllObj) && clearAllObj is Func<Task> clearAllCallback)
        {
            _onClearAllCallback = clearAllCallback;
        }

        LoadActions();
    }

    private void LoadActions()
    {
        Items.Clear();
        if (_sourceActions == null && _sourceTextElements == null) return;

        var orderedItems = new List<(long Order, CanvasActionViewModel? CanvasAction, TextElementViewModel? TextElement)>();

        if (_sourceActions != null)
        {
            orderedItems.AddRange(_sourceActions
                .Where(action => action is not TextSnapshotCanvasActionViewModel)
                .Select(action => (Order: (long)action.Order, CanvasAction: (CanvasActionViewModel?)action, TextElement: (TextElementViewModel?)null)));
        }

        if (_sourceTextElements != null)
        {
            orderedItems.AddRange(_sourceTextElements
                .Select(textElement => (Order: textElement.Order, CanvasAction: (CanvasActionViewModel?)null, TextElement: (TextElementViewModel?)textElement)));
        }

        bool foundTopSnapshot = false;

        foreach (var entry in orderedItems.OrderByDescending(entry => entry.Order))
        {
            var item = _serviceProvider.GetRequiredService<ICanvasHistoryItemViewModel>();

            if (entry.CanvasAction != null)
            {
                item.InitWith(entry.CanvasAction, OnDeleteItem, OnDuplicateItem);
            }
            else if (entry.TextElement != null)
            {
                item.InitWith(entry.TextElement, OnDeleteItem, OnDuplicateItem);
            }
            else
            {
                continue;
            }
            
            if (item.IsSnapshot)
            {
                if (!foundTopSnapshot)
                {
                    item.IsDeletable = true;
                    foundTopSnapshot = true;
                }
                else
                {
                    item.IsDeletable = false;
                }
            }
            else
            {
                item.IsDeletable = true;
            }

            Items.Add(item);
        }
    }

    private void OnDuplicateItem(ICanvasHistoryItemViewModel item)
    {
        if (item.TextElement != null)
        {
            if (_onTextDuplicateCallback != null)
            {
                _onTextDuplicateCallback.Invoke(item.TextElement);
            }
            else if (_sourceTextElements != null)
            {
                var nextOrder = getNextOrder();
                _sourceTextElements.Add(new TextElementViewModel(Guid.NewGuid().ToString(), nextOrder, item.TextElement.BaseFontSize)
                {
                    Text = item.TextElement.Text,
                    X = item.TextElement.X,
                    Y = item.TextElement.Y,
                    Scale = item.TextElement.Scale,
                    Rotation = item.TextElement.Rotation,
                    Color = item.TextElement.Color,
                    Alpha = item.TextElement.Alpha
                });
            }

            LoadActions();
            return;
        }

        if (item.CanvasAction == null || _sourceActions == null || item.IsSnapshot) return;

        if (_onActionDuplicateCallback != null)
        {
            _onActionDuplicateCallback.Invoke(item.CanvasAction);
            LoadActions();
            return;
        }

        var duplicatedAction = item.CanvasAction.Clone();
        duplicatedAction.Order = checked((int)getNextOrder());
        _sourceActions.Add(duplicatedAction);

        LoadActions();
    }

    private long getNextOrder()
    {
        var nextCanvasActionOrder = _sourceActions?.Count > 0
            ? _sourceActions.Max(canvasAction => canvasAction.Order) + 1L
            : 0L;
        var nextTextOrder = _sourceTextElements?.Count > 0
            ? _sourceTextElements.Max(textElement => textElement.Order) + 1L
            : 0L;

        return Math.Max(nextCanvasActionOrder, nextTextOrder);
    }

    private async void OnDeleteItem(ICanvasHistoryItemViewModel item)
    {
        if (item.TextElement != null)
        {
            if (_onTextDeleteCallback != null)
            {
                await _onTextDeleteCallback.Invoke(item.TextElement);
            }
            else
            {
                _sourceTextElements?.Remove(item.TextElement);
            }

            LoadActions();
            return;
        }

        if (item.CanvasAction != null && _sourceActions != null)
        {
            if (item.IsSnapshot && item.CanvasAction is SnapshotCanvasActionViewModel snapshotAction)
            {
                if (_onSnapshotDeleteCallback != null)
                {
                    await _onSnapshotDeleteCallback.Invoke(snapshotAction);
                }
                Items.Remove(item);
                LoadActions(); // Reload to update "IsDeletable" for the next topmost snapshot
                return;
            }

            if (_onActionDeleteCallback != null)
            {
                await _onActionDeleteCallback.Invoke(item.CanvasAction);
            }
            else
            {
                _sourceActions.Remove(item.CanvasAction);
            }
            
            // Reload to ensure valid state
            LoadActions();
        }
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        var result = await _popupService.DisplayAlertAsync("Clear all?", "This will clear all history including image undo checkpoints. Continue?", "YES", "Cancel");
        if (result)
        {
            if (_onClearAllCallback != null)
            {
                await _onClearAllCallback.Invoke();
            }
            else
            {
                if (_sourceActions != null) _sourceActions.Clear();
                await _canvasHistoryService.ClearAllAsync();
            }
            
            Items.Clear();

            await ClosePopupAsync();
        }
    }

    [RelayCommand]
    private async Task ClearMasks()
    {
        var result = await _popupService.DisplayAlertAsync("Clear masks?", "This will clear all masks but preserve all other items. Continue?", "YES", "Cancel");
        if (result)
        {
            if (_sourceActions == null) return;

            var toRemove = _sourceActions.Where(action => action is MaskLineViewModel or SegmentationMaskViewModel).ToList();
            foreach (var a in toRemove)
            {
                _sourceActions.Remove(a);
            }
            
            LoadActions();
        }
    }

    [RelayCommand]
    private async Task Close()
    {
        await ClosePopupAsync();
    }

}