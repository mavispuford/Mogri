using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;
using Mogri.Interfaces.Services;
using Mogri.Enums;
using Mogri.Messages;

namespace Mogri.ViewModels;

public partial class EditMasksPopupViewModel : PopupBaseViewModel, IEditMasksPopupViewModel, IRecipient<MaskSliderDragMessage>
{
    private readonly IServiceProvider _serviceProvider;
    private ObservableCollection<CanvasActionViewModel>? _sourceActions;

    [ObservableProperty]
    private ObservableCollection<IEditMaskItemViewModel> _items = new();

    public EditMasksPopupViewModel(IPopupService popupService, IServiceProvider serviceProvider) : base(popupService)
    {
        _serviceProvider = serviceProvider;
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
            LoadActions();
        }
    }

    private void LoadActions()
    {
        Items.Clear();
        if (_sourceActions == null) return;

        var filtered = _sourceActions
            .Where(a =>
                (a is MaskLineViewModel m && (m.MaskEffect == MaskEffect.Paint || m.MaskEffect == MaskEffect.Erase)) ||
                (a is SegmentationMaskViewModel))
            .Reverse();

        foreach (var action in filtered)
        {
            var item = _serviceProvider.GetRequiredService<IEditMaskItemViewModel>();
            item.InitWith(action, OnDeleteItem, OnDuplicateItem);
            Items.Add(item);
        }
    }

    private void OnDuplicateItem(IEditMaskItemViewModel item)
    {
        if (item.CanvasAction == null || _sourceActions == null) return;

        var index = _sourceActions.IndexOf(item.CanvasAction);
        if (index >= 0)
        {
            var result = item.CanvasAction.Clone();
            _sourceActions.Insert(index + 1, result);

            var newItem = _serviceProvider.GetRequiredService<IEditMaskItemViewModel>();
            newItem.InitWith(result, OnDeleteItem, OnDuplicateItem);

            var itemIndex = Items.IndexOf(item);
            if (itemIndex >= 0)
            {
                Items.Insert(itemIndex, newItem);
            }
        }
    }

    private void OnDeleteItem(IEditMaskItemViewModel item)
    {
        if (item.CanvasAction != null && _sourceActions != null)
        {
            _sourceActions.Remove(item.CanvasAction);
            Items.Remove(item);
        }
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        var result = await Shell.Current.DisplayAlertAsync("Clear mask?", "Are you sure you would like to clear all items?", "YES", "Cancel");
        if (result)
        {
            if (_sourceActions == null || !_sourceActions.Any()) return;

            _sourceActions.Clear();
            Items.Clear();

            await ClosePopupAsync();
        }
    }
}
