using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Enums;

namespace MobileDiffusion.ViewModels;

public partial class EditMasksPopupViewModel : PopupBaseViewModel, IEditMasksPopupViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private ObservableCollection<CanvasActionViewModel> _sourceActions;

    [ObservableProperty]
    private ObservableCollection<IEditMaskItemViewModel> _items = new();

    public EditMasksPopupViewModel(IPopupService popupService, IServiceProvider serviceProvider) : base(popupService)
    {
        _serviceProvider = serviceProvider;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
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
                (a is MaskLineViewModel m && m.MaskEffect == MaskEffect.Paint) ||
                (a is SegmentationMaskViewModel))
            .Reverse();

        foreach (var action in filtered)
        {
            var item = _serviceProvider.GetRequiredService<IEditMaskItemViewModel>();
            item.InitWith(action, OnDeleteItem);
            Items.Add(item);
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
        }
    }
}
