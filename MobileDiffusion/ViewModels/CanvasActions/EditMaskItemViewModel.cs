using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using MobileDiffusion;

namespace MobileDiffusion.ViewModels;

public partial class EditMaskItemViewModel : ObservableObject, IEditMaskItemViewModel
{
    private readonly IPopupService _popupService;
    private Action<IEditMaskItemViewModel>? _deleteAction;
    private Action<IEditMaskItemViewModel>? _duplicateAction;

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

    public EditMaskItemViewModel(IPopupService popupService)
    {
        _popupService = popupService;
    }

    public void InitWith(CanvasActionViewModel canvasAction, Action<IEditMaskItemViewModel> deleteAction, Action<IEditMaskItemViewModel> duplicateAction)
    {
        _deleteAction = deleteAction;
        _duplicateAction = duplicateAction;
        CanvasAction = canvasAction;

        initialize();
    }

    private void initialize()
    {
        if (CanvasAction is MaskLineViewModel maskLine)
        {
            if (maskLine.MaskEffect == MaskEffect.Erase)
            {
                Icon = "\ue6d0"; // Erase
                IsColorVisible = false;
                DisplayColor = Colors.Transparent; // Or generic color since not visible
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
            Icon = "\ue997"; // Paint Bucket
            IsColorVisible = true;
            DisplayColor = segMask.Color;
            Alpha = segMask.Alpha;
        }

        updateDescription();
        updateColorWithAlpha();

        if (CanvasAction != null)
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
        if (CanvasAction == null) return;

        var parameters = new Dictionary<string, object>
        {
            { "Action", CanvasAction }
        };

        await _popupService.ShowPopupAsync("EditMaskItemPopup", parameters);
    }

    [RelayCommand]
    private void Delete()
    {
        _deleteAction?.Invoke(this);
    }

    [RelayCommand]
    private void Duplicate()
    {
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
