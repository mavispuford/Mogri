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
    private Func<SnapshotCanvasActionViewModel, Task>? _onSnapshotDeleteCallback;
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
        
        if (query.TryGetValue("OnSnapshotDelete", out var callbackObj) && callbackObj is Func<SnapshotCanvasActionViewModel, Task> callback)
        {
            _onSnapshotDeleteCallback = callback;
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
        if (_sourceActions == null) return;

        var filtered = _sourceActions.Reverse().ToList();
        
        bool foundTopSnapshot = false;

        foreach (var action in filtered)
        {
            var item = _serviceProvider.GetRequiredService<ICanvasHistoryItemViewModel>();
            item.InitWith(action, OnDeleteItem, OnDuplicateItem);
            
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
        if (item.CanvasAction == null || _sourceActions == null || item.IsSnapshot) return;

        var index = _sourceActions.IndexOf(item.CanvasAction);
        if (index >= 0)
        {
            var result = item.CanvasAction.Clone();
            _sourceActions.Insert(index + 1, result);

            var newItem = _serviceProvider.GetRequiredService<ICanvasHistoryItemViewModel>();
            newItem.InitWith(result, OnDeleteItem, OnDuplicateItem);

            var itemIndex = Items.IndexOf(item);
            if (itemIndex >= 0)
            {
                // We add before since list is reversed
                Items.Insert(itemIndex, newItem);
            }
        }
    }

    private async void OnDeleteItem(ICanvasHistoryItemViewModel item)
    {
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

            _sourceActions.Remove(item.CanvasAction);
            Items.Remove(item);
            
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

            var toRemove = _sourceActions.Where(a => a is not SnapshotCanvasActionViewModel).ToList();
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
