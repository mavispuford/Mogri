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
    private Action<IEditMaskItemViewModel> _deleteAction;
    private Action<IEditMaskItemViewModel> _duplicateAction;

    [ObservableProperty]
    private CanvasActionViewModel _canvasAction;

    [ObservableProperty]
    private string _icon;

    [ObservableProperty]
    private Color _displayColor;

    [ObservableProperty]
    private double _alpha;

    [ObservableProperty]
    private Color _colorWithAlpha;

    public EditMaskItemViewModel(IPopupService popupService)
    {
        _popupService = popupService;
    }

    public void InitWith(CanvasActionViewModel canvasAction, Action<IEditMaskItemViewModel> deleteAction, Action<IEditMaskItemViewModel> duplicateAction)
    {
        _deleteAction = deleteAction;
        _duplicateAction = duplicateAction;
        CanvasAction = canvasAction;

        Initialize();
    }

    private void Initialize()
    {
        if (CanvasAction is MaskLineViewModel maskLine)
        {
            Icon = "\ue3ae"; // Brush
            DisplayColor = maskLine.Color;
            Alpha = maskLine.Alpha;
        }
        else if (CanvasAction is SegmentationMaskViewModel segMask)
        {
            Icon = "\ue997"; // Paint Bucket
            DisplayColor = segMask.Color;
            Alpha = segMask.Alpha;
        }

        UpdateColorWithAlpha();

        if (CanvasAction != null)
        {
            CanvasAction.PropertyChanged += Action_PropertyChanged;
        }
    }

    partial void OnAlphaChanged(double value)
    {
        UpdateColorWithAlpha();
        
        if (CanvasAction is MaskLineViewModel maskLine)
        {
            maskLine.Alpha = (float)value;
        }
        else if (CanvasAction is SegmentationMaskViewModel segMask)
        {
            segMask.Alpha = (float)value;
        }
    }

    partial void OnDisplayColorChanged(Color value)
    {
        UpdateColorWithAlpha();
    }

    private void UpdateColorWithAlpha()
    {
        if (DisplayColor != null)
        {
            ColorWithAlpha = DisplayColor.WithAlpha((float)Alpha);
        }
    }

    private void Action_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
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
    }

    [RelayCommand]
    private async Task ChangeColor()
    {
        if (_popupService == null) return;
        
        var current = DisplayColor;
        var parameters = new Dictionary<string, object>
        {
            { NavigationParams.Color, current }
        };

        var result = await _popupService.ShowPopupForResultAsync("ColorPickerPopup", parameters);

        if (result is Color newColor)
        {
            DisplayColor = newColor;
            if (CanvasAction is MaskLineViewModel m) m.Color = newColor;
            else if (CanvasAction is SegmentationMaskViewModel s) s.Color = newColor;
        }
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
}
