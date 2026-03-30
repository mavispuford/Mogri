using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using Mogri.Enums;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mogri.ViewModels;

/// <summary>
/// Represents a single row in the canvas history popup, handling display
/// and interaction for mask strokes, segmentation masks, and snapshot checkpoints.
/// </summary>
public partial class HistoryItemActionViewModel : ObservableObject, IHistoryItemActionViewModel
{
    private readonly IPopupService _popupService;
    private Action<IHistoryItemActionViewModel>? _deleteAction;
    private Action<IHistoryItemActionViewModel>? _duplicateAction;

    [ObservableProperty]
    private CanvasActionViewModel? _canvasAction;

    [ObservableProperty]
    private string _icon = string.Empty;

    [ObservableProperty]
    private bool _isColorVisible;

    [ObservableProperty]
    private Color _displayColor = Colors.Transparent;

    [ObservableProperty]
    private double _alpha;

    [ObservableProperty]
    private Color _colorWithAlpha = Colors.Transparent;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isSnapshot;

    [ObservableProperty]
    private bool _isEditable;

    [ObservableProperty]
    private bool _isDuplicable;

    [ObservableProperty]
    private bool _isDeletable;

    public HistoryItemActionViewModel(IPopupService popupService)
    {
        _popupService = popupService;
    }

    public void InitWith(CanvasActionViewModel canvasAction, Action<IHistoryItemActionViewModel> deleteAction, Action<IHistoryItemActionViewModel> duplicateAction)
    {
        _deleteAction = deleteAction;
        _duplicateAction = duplicateAction;
        CanvasAction = canvasAction;

        initialize();
    }

    private void initialize()
    {
        if (CanvasAction is SnapshotCanvasActionViewModel snapshotAction)
        {
            Icon = "\ue411"; // History icon
            IsColorVisible = false;
            DisplayColor = Colors.Transparent;
            Alpha = 1.0;
            IsSnapshot = true;
            IsEditable = false;
            IsDuplicable = false;
            // Deletable is set by the parent popup for only the top-most snapshot
            IsDeletable = false;
            Description = snapshotAction.Description;
        }
        else if (CanvasAction is MaskLineViewModel maskLine)
        {
            IsSnapshot = false;
            IsEditable = true;
            IsDuplicable = true;
            IsDeletable = true;

            if (maskLine.MaskEffect == MaskEffect.Erase)
            {
                Icon = "\ue6d0"; // Erase
                IsColorVisible = false;
                DisplayColor = Colors.Transparent;
                Alpha = 1.0;
            }
            else
            {
                Icon = "\ue3ae"; // Brush
                IsColorVisible = true;
                DisplayColor = maskLine.Color;
                Alpha = maskLine.Alpha;
            }
        }
        else if (CanvasAction is SegmentationMaskViewModel segMask)
        {
            IsSnapshot = false;
            IsEditable = true;
            IsDuplicable = true;
            IsDeletable = true;

            Icon = "\ue997"; // Paint Bucket
            IsColorVisible = true;
            DisplayColor = segMask.Color;
            Alpha = segMask.Alpha;
        }

        if (!IsSnapshot)
        {
            updateDescription();
        }
        
        updateColorWithAlpha();

        if (CanvasAction != null && !IsSnapshot)
        {
            CanvasAction.PropertyChanged += action_PropertyChanged;
        }
    }

    partial void OnAlphaChanged(double value)
    {
        updateColorWithAlpha();
    }

    partial void OnDisplayColorChanged(Color value)
    {
        updateColorWithAlpha();
    }

    private void updateColorWithAlpha()
    {
        if (DisplayColor != null)
        {
            ColorWithAlpha = DisplayColor.WithAlpha((float)Alpha);
        }
    }

    private void action_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Color")
        {
            if (CanvasAction is MaskLineViewModel maskLine)
            {
                DisplayColor = maskLine.Color;
            }
            else if (CanvasAction is SegmentationMaskViewModel segMask)
            {
                DisplayColor = segMask.Color;
            }
        }
        else if (e.PropertyName == "Alpha")
        {
            if (CanvasAction is MaskLineViewModel maskLine)
            {
                Alpha = maskLine.Alpha;
            }
            else if (CanvasAction is SegmentationMaskViewModel segMask)
            {
                Alpha = segMask.Alpha;
            }

            updateDescription();
        }
        else if (e.PropertyName == "BrushSize")
        {
            updateDescription();
        }
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (CanvasAction == null || !IsEditable) return;

        var parameters = new Dictionary<string, object>
        {
            { "Action", CanvasAction }
        };

        await _popupService.ShowPopupAsync("EditMaskItemPopup", parameters);
    }

    [RelayCommand]
    private void Delete()
    {
        if (!IsDeletable) return;
        _deleteAction?.Invoke(this);
    }

    [RelayCommand]
    private void Duplicate()
    {
        if (!IsDuplicable) return;
        _duplicateAction?.Invoke(this);
    }

    private void updateDescription()
    {
        if (CanvasAction is MaskLineViewModel maskLine)
        {
            if (maskLine.MaskEffect == MaskEffect.Erase)
            {
                Description = $"Size {maskLine.BrushSize / (maskLine.TouchScale == 0 ? 1 : maskLine.TouchScale):F0}";
            }
            else
            {
                Description = $"{maskLine.Alpha:P0}, Size {maskLine.BrushSize / (maskLine.TouchScale == 0 ? 1 : maskLine.TouchScale):F0}";
            }
        }
        else if (CanvasAction is SegmentationMaskViewModel segMask)
        {
            Description = $"{segMask.Alpha:P0}";
        }
    }
}
