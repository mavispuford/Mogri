using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;
using Mogri.Interfaces.Services;
using Mogri.Messages;

namespace Mogri.ViewModels;

public partial class EditMaskItemPopupViewModel : PopupBaseViewModel, IEditMaskItemPopupViewModel, IRecipient<MaskSliderDragMessage>
{
    private CanvasActionViewModel? _action;
    private TextElementViewModel? _textElement;

    [ObservableProperty]
    private string _title = "Edit Mask";

    [ObservableProperty]
    private bool _isBrush;

    [ObservableProperty]
    private bool _isColorVisible;

    [ObservableProperty]
    private double _brushSize;

    [ObservableProperty]
    private double _noise;

    [ObservableProperty]
    private float _alpha;

    [ObservableProperty]
    private Color _displayColor = Colors.Transparent;

    [ObservableProperty]
    private bool _isNoiseVisible;

    [ObservableProperty]
    private bool _isDragging;

    [ObservableProperty]
    private string _dragInfoText = string.Empty;

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
        _textElement = null;
        Title = "Edit Mask";

        if (_action is MaskLineViewModel line)
        {
            IsBrush = true;
            IsColorVisible = line.MaskEffect != Enums.MaskEffect.Erase;
            IsNoiseVisible = line.MaskEffect != Enums.MaskEffect.Erase;

            var scale = (line.TouchScale <= 0) ? 1 : line.TouchScale;
            BrushSize = line.BrushSize / scale;
            Alpha = line.Alpha;
            Noise = line.Noise;
            DisplayColor = line.Color;
        }
        else if (_action is SegmentationMaskViewModel seg)
        {
            IsBrush = false;
            IsColorVisible = true;
            IsNoiseVisible = true;
            Alpha = seg.Alpha;
            Noise = seg.Noise;
            DisplayColor = seg.Color;
            BrushSize = 0;
        }
    }

    public void InitWith(TextElementViewModel textElement)
    {
        _action = null;
        _textElement = textElement;
        Title = "Edit Text";
        IsBrush = false;
        IsColorVisible = true;
        IsNoiseVisible = true;
        Alpha = textElement.Alpha;
        DisplayColor = textElement.Color;
        Noise = textElement.Noise;
        BrushSize = 0;
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);
        if (query.TryGetValue("Action", out var actionObj) && actionObj is CanvasActionViewModel action)
        {
            InitWith(action);
        }
        else if (query.TryGetValue("TextElement", out var textElementObj) && textElementObj is TextElementViewModel textElement)
        {
            InitWith(textElement);
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
        else if (_textElement != null)
        {
            _textElement.Alpha = value;
        }
    }

    partial void OnNoiseChanged(double value)
    {
        DragInfoText = $"Noise: {value:P0}";

        if (_action is MaskLineViewModel line)
        {
            line.Noise = value;
        }
        else if (_action is SegmentationMaskViewModel seg)
        {
            seg.Noise = value;
        }
        else if (_textElement != null)
        {
            _textElement.Noise = value;
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
            else if (_textElement != null)
            {
                _textElement.Color = color;
            }
        }
    }

    [RelayCommand]
    private async Task Close()
    {
        await ClosePopupAsync();
    }

}