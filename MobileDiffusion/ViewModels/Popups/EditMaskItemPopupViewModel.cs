using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Messages;

namespace MobileDiffusion.ViewModels;

public partial class EditMaskItemPopupViewModel : PopupBaseViewModel, IEditMaskItemPopupViewModel, IRecipient<MaskSliderDragMessage>
{
    private CanvasActionViewModel _action;

    [ObservableProperty]
    private bool _isBrush;

    [ObservableProperty]
    private double _brushSize;

    [ObservableProperty]
    private float _alpha;

    [ObservableProperty]
    private Color _displayColor;

    [ObservableProperty]
    private bool _isDragging;

    [ObservableProperty]
    private string _dragInfoText;

    public EditMaskItemPopupViewModel(IPopupService popupService) : base(popupService)
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    [RelayCommand]
    private void OnDragStarted()
    {
        WeakReferenceMessenger.Default.Send(new MaskSliderDragMessage(true));
    }

    [RelayCommand]
    private void OnDragCompleted()
    {
        WeakReferenceMessenger.Default.Send(new MaskSliderDragMessage(false));
    }

    public void Receive(MaskSliderDragMessage message)
    {
        // Must keep opacity slightly above 0 to maintain touch interaction with the slider
        ContentOpacity = message.Value ? 0 : 1;
        IsDragging = message.Value;
        
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

    public void InitWith(CanvasActionViewModel action)
    {
        _action = action;
        
        IsBrush = _action is MaskLineViewModel;

        if (_action is MaskLineViewModel line)
        {
            var scale = (line.TouchScale <= 0) ? 1 : line.TouchScale;
            BrushSize = line.BrushSize / scale;
            Alpha = line.Alpha;
            DisplayColor = line.Color;
        }
        else if (_action is SegmentationMaskViewModel seg)
        {
            Alpha = seg.Alpha;
            DisplayColor = seg.Color;
            BrushSize = 0; 
        }
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);
        if (query.TryGetValue("Action", out var actionObj) && actionObj is CanvasActionViewModel action)
        {
            InitWith(action);
        }
    }

    partial void OnBrushSizeChanged(double value)
    {
        DragInfoText = $"Size: {value:F0}";

        if (_action is MaskLineViewModel line)
        {
            var scale = (line.TouchScale <= 0) ? 1 : line.TouchScale;
            line.BrushSize = (float)value * scale;
        }
    }

    partial void OnAlphaChanged(float value)
    {
        DragInfoText = $"Opacity: {value:P0}";

        if (_action is MaskLineViewModel line)
        {
            line.Alpha = value;
        }
        else if (_action is SegmentationMaskViewModel seg)
        {
            seg.Alpha = value;
        }
    }

    [RelayCommand]
    private async Task ChangeColor()
    {
        var parameters = new Dictionary<string, object>();
        // Using explicit struct check if possible, or just null check. Color is a class in MAUI.
        if (DisplayColor != null)
        {
            parameters.Add(NavigationParams.Color, DisplayColor);
        }

        var result = await _popupService.ShowPopupForResultAsync("ColorPickerPopup", parameters);
        
        if (result is Color color)
        {
            DisplayColor = color;
            if (_action is MaskLineViewModel line)
            {
                line.Color = color;
            }
            else if (_action is SegmentationMaskViewModel seg)
            {
                seg.Color = color;
            }
        }
    }

    [RelayCommand]
    private async Task Close()
    {
        await ClosePopupAsync();
    }
}
