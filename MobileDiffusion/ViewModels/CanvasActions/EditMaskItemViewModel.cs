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

    [ObservableProperty]
    private CanvasActionViewModel _canvasAction;

    [ObservableProperty]
    private string _icon;

    [ObservableProperty]
    private Color _displayColor;

    public EditMaskItemViewModel(IPopupService popupService)
    {
        _popupService = popupService;
    }

    public void InitWith(CanvasActionViewModel canvasAction, Action<IEditMaskItemViewModel> deleteAction)
    {
        _deleteAction = deleteAction;
        CanvasAction = canvasAction;

        Initialize();
    }

    private void Initialize()
    {
        if (CanvasAction is MaskLineViewModel maskLine)
        {
            Icon = "\ue3ae"; // Brush
            DisplayColor = maskLine.Color;
        }
        else if (CanvasAction is SegmentationMaskViewModel segMask)
        {
            Icon = "\ue997"; // Paint Bucket
            DisplayColor = segMask.Color;
        }

        if (CanvasAction != null)
        {
            CanvasAction.PropertyChanged += Action_PropertyChanged;
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
}
