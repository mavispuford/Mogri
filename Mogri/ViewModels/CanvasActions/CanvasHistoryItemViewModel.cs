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
public partial class CanvasHistoryItemViewModel : ObservableObject, ICanvasHistoryItemViewModel
{
    private readonly IPopupService _popupService;
    private Action<ICanvasHistoryItemViewModel>? _deleteAction;
    private Action<ICanvasHistoryItemViewModel>? _duplicateAction;

    [ObservableProperty]
    private CanvasActionViewModel? _canvasAction;

    [ObservableProperty]
    private TextElementViewModel? _textElement;

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

    public CanvasHistoryItemViewModel(IPopupService popupService)
    {
        _popupService = popupService;
    }

    public void InitWith(CanvasActionViewModel canvasAction, Action<ICanvasHistoryItemViewModel> deleteAction, Action<ICanvasHistoryItemViewModel> duplicateAction)
    {
        _deleteAction = deleteAction;
        _duplicateAction = duplicateAction;
        TextElement = null;
        CanvasAction = canvasAction;

        initialize();
    }

    public void InitWith(TextElementViewModel textElement, Action<ICanvasHistoryItemViewModel> deleteAction, Action<ICanvasHistoryItemViewModel> duplicateAction)
    {
        _deleteAction = deleteAction;
        _duplicateAction = duplicateAction;
        CanvasAction = null;
        TextElement = textElement;

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
        else if (TextElement != null)
        {
            IsSnapshot = false;
            IsEditable = true;
            IsDuplicable = true;
            IsDeletable = true;
            Icon = "\uea1e"; // Text
            IsColorVisible = true;
            DisplayColor = TextElement.Color;
            Alpha = TextElement.Alpha;
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

        if (TextElement != null)
        {
            TextElement.PropertyChanged += textElement_PropertyChanged;
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

    private void textElement_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (TextElement == null)
        {
            return;
        }

        if (e.PropertyName == nameof(TextElementViewModel.Color))
        {
            DisplayColor = TextElement.Color;
        }
        else if (e.PropertyName == nameof(TextElementViewModel.Alpha))
        {
            Alpha = TextElement.Alpha;
            updateDescription();
        }
        else if (e.PropertyName == nameof(TextElementViewModel.Noise))
        {
            updateDescription();
        }
        else if (e.PropertyName == nameof(TextElementViewModel.Text))
        {
            updateDescription();
        }
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (!IsEditable) return;

        var parameters = new Dictionary<string, object>();

        if (CanvasAction != null)
        {
            parameters.Add("Action", CanvasAction);
        }
        else if (TextElement != null)
        {
            parameters.Add("TextElement", TextElement);
        }
        else
        {
            return;
        }

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
        else if (TextElement != null)
        {
            var preview = createTextPreview(TextElement.Text);
            Description = $"{preview}, {TextElement.Alpha:P0}, Noise {TextElement.Noise:P0}";
        }
    }

    private static string createTextPreview(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Text";
        }

        var flattened = string.Join(" ", text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (flattened.Length > 18)
        {
            flattened = flattened[..18] + "...";
        }

        return $"\"{flattened}\"";
    }
}
