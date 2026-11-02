using Mogri.Enums;

namespace Mogri.Views;

/// <summary>
/// Canvas page partial that manages slider chrome, action-tray animation, haptics, timers, and tool-context button visibility.
/// </summary>
public partial class CanvasPage
{
    // Auto-hide UI chrome.
    private enum ContextSliderType
    {
        None,
        BrushSize,
        MaskPadding,
        Alpha,
        Noise
    }

    private ContextSliderType _activeSliderType = ContextSliderType.None;
    private bool _isConfiguringSlider;
    private Timer? _sliderTimer;

    // Page-level chrome state.
    private bool _hapticsEnabled = false;

    private void ActionsContainer_SizeChanged(object? sender, EventArgs e)
    {
        if (ActionsContainer.Height < 0)
        {
            return;
        }

        AnimateActionsContainer(ShowActions, false);
    }

    private async void AnimateActionsContainer(bool show, bool animate = true)
    {
        if (show)
        {
            if (animate)
            {
                await ActionsContainer.TranslateToAsync(0, 0, 200, Easing.CubicInOut);
            }
            else
            {
                ActionsContainer.TranslationY = 0;
            }
        }
        else
        {
            // Calculate height dynamically
            var translation = (ActionsContainer.Height / 4);
            if (translation <= 0) translation = 200; // fallback if not measured

            if (animate)
            {
                await ActionsContainer.TranslateToAsync(0, translation, 200, Easing.CubicInOut);
            }
            else
            {
                ActionsContainer.TranslationY = translation;
            }
        }
    }

    private void Brush_Size_Button_Clicked(object? sender, EventArgs e)
    {
        ToggleSlider(ContextSliderType.BrushSize, BrushSizeButton);
    }

    private void Mask_Padding_Button_Clicked(object? sender, EventArgs e)
    {
        ToggleSlider(ContextSliderType.MaskPadding, MaskPaddingButton);
    }

    private void Alpha_Button_Clicked(object? sender, EventArgs e)
    {
        ToggleSlider(ContextSliderType.Alpha, AlphaButton);
    }

    private void Noise_Button_Clicked(object? sender, EventArgs e)
    {
        ToggleSlider(ContextSliderType.Noise, NoiseButton);
    }

    private async void ToggleSlider(ContextSliderType type, VisualElement anchorButton)
    {
        vibrate(HapticFeedbackType.Click);

        if (_activeSliderType == type && ActiveSliderContainer.IsVisible)
        {
            ShowHideActiveSlider(false);
            return;
        }

        _activeSliderType = type;
        ConfigureActiveSlider(type);

        var sliderWidth = ActiveSliderContainer.Width > 0 ? ActiveSliderContainer.Width : 230;
        var targetX = calculateSliderTargetX(anchorButton, sliderWidth);

        if (!ActiveSliderContainer.IsVisible)
        {
            ActiveSliderContainer.TranslationX = targetX;
            ShowHideActiveSlider(true);
        }
        else
        {
            ResetSliderTimer();
            ActiveSliderContainer.AbortAnimation("SlideToX");
            await ActiveSliderContainer.TranslateToAsync(targetX, 0, 180, Easing.CubicOut);
        }
    }

    private double calculateSliderTargetX(VisualElement button, double sliderWidth)
    {
        var buttonCenter = ContextButtonsLayout.X + button.X + (button.Width / 2);
        var targetX = buttonCenter - (sliderWidth / 2);
        var minX = 10 - getTotalHeirarchicalSidePadding(ContextButtonsLayout, true);
        var totalRightPadding = getTotalHeirarchicalSidePadding(ContextButtonsLayout, false);
        var maxX = Math.Max(minX, Width - sliderWidth - 10 - totalRightPadding);

        return Math.Clamp(targetX, minX, maxX);
    }

    private double getTotalHeirarchicalSidePadding(Layout layout, bool left, double currentTotal = 0, int iteration = 0)
    {
        if (layout == null || iteration > 10)
        {
            return currentTotal;
        }

        currentTotal += left ? layout.Padding.Left : layout.Padding.Right;

        return getTotalHeirarchicalSidePadding(layout.Parent as Layout, left, currentTotal, iteration++);
    }

    private void ConfigureActiveSlider(ContextSliderType type)
    {
        _isConfiguringSlider = true;
        try
        {
            switch (type)
            {
                case ContextSliderType.BrushSize:
                    ActiveSlider.Minimum = 1;
                    ActiveSlider.Maximum = 50;
                    ActiveSlider.Value = CurrentBrushSize;
                    ActiveSliderLabel.Text = $"Size: {CurrentBrushSize:F0}";
                    break;

                case ContextSliderType.MaskPadding:
                    ActiveSlider.Minimum = -5;
                    ActiveSlider.Maximum = 15;
                    ActiveSlider.Value = SegmentationPadding;
                    ActiveSliderLabel.Text = $"Padding: {SegmentationPadding:+#;-#;0} px";
                    break;

                case ContextSliderType.Alpha:
                    ActiveSlider.Minimum = 0.01;
                    ActiveSlider.Maximum = 1.0;
                    ActiveSlider.Value = CurrentAlpha;
                    ActiveSliderLabel.Text = $"Alpha: {CurrentAlpha:F2}";
                    break;

                case ContextSliderType.Noise:
                    ActiveSlider.Minimum = 0;
                    ActiveSlider.Maximum = 1;
                    ActiveSlider.Value = CurrentNoise;
                    ActiveSliderLabel.Text = $"Noise: {CurrentNoise:P0}";
                    break;
            }
        }
        finally
        {
            _isConfiguringSlider = false;
        }
    }

    private void ActiveSlider_ValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isConfiguringSlider)
        {
            return;
        }

        switch (_activeSliderType)
        {
            case ContextSliderType.BrushSize:
                CurrentBrushSize = e.NewValue;
                ActiveSliderLabel.Text = $"Size: {e.NewValue:F0}";
                break;

            case ContextSliderType.MaskPadding:
                var intVal = (int)Math.Round(e.NewValue);
                SegmentationPadding = intVal;
                ActiveSliderLabel.Text = $"Padding: {intVal:+#;-#;0} px";
                break;

            case ContextSliderType.Alpha:
                CurrentAlpha = e.NewValue;
                ActiveSliderLabel.Text = $"Alpha: {e.NewValue:F2}";
                break;

            case ContextSliderType.Noise:
                CurrentNoise = e.NewValue;
                ActiveSliderLabel.Text = $"Noise: {e.NewValue:P0}";
                break;
        }

        ResetSliderTimer();
    }

    private void ShowHideActiveSlider(bool show)
    {
        if (show)
        {
            ActiveSliderContainer.Opacity = 1;
            ActiveSliderContainer.IsVisible = true;
            ResetSliderTimer();
        }
        else
        {
            ActiveSliderContainer.Opacity = 0;
            ActiveSliderContainer.IsVisible = false;
            _activeSliderType = ContextSliderType.None;
        }
    }

    public void ResetSliderTimer()
    {
        if (_sliderTimer == null)
        {
            _sliderTimer = new Timer(delegate
            {
                Dispatcher.Dispatch(() =>
                {
                    ShowHideActiveSlider(false);
                });
            }, null, 5000, -1);
        }
        else
        {
            _sliderTimer.Change(5000, -1);
        }
    }

    public void AutoHideBrushSizeSlider() => ResetSliderTimer();
    public void AutoHideAlphaSlider() => ResetSliderTimer();
    public void AutoHideNoiseSlider() => ResetSliderTimer();
    public void AutoHideMaskPaddingSlider() => ResetSliderTimer();

    private void HideSliders()
    {
        ActiveSliderContainer.IsVisible = false;
        _activeSliderType = ContextSliderType.None;
    }

    private void UpdateMaskLayer()
    {
        MaskCanvasView.AbortAnimation("FadeInOutMaskCanvasView");
        MaskCanvasView.Animate("FadeInOutMaskCanvasView", value => MaskCanvasView.Opacity = value, MaskCanvasView.Opacity, ShowMaskLayer ? 1 : 0, easing: Easing.CubicInOut);
    }

    private void ToolCollectionView_SelectedItemChanged(object? sender, SelectionChangedEventArgs e)
    {
        vibrate(HapticFeedbackType.Click);
    }

    private void vibrate(HapticFeedbackType type)
    {
        if (_hapticsEnabled &&
            HapticFeedback.Default.IsSupported)
        {
            HapticFeedback.Default.Perform(type);
        }
    }

    private void Vibrate_Button_Tapped(object? sender, TappedEventArgs e)
    {
        vibrate(HapticFeedbackType.Click);
    }

    private void OnCurrentToolChanged()
    {
        if (CurrentTool?.ContextButtons == null)
        {
            resetTextInteractionState(clearSelection: true, clearTapState: true);
            ShowBoundingBox = false;
            updateTextFlipButtonsVisibility();
            return;
        }

        if (CurrentTool.Type != ToolType.Text)
        {
            resetTextInteractionState(clearSelection: true, clearTapState: true);
        }

        ShowBoundingBox = CurrentTool.Type == ToolType.BoundingBox;

        if (ShowBoundingBox && !ShowActions)
        {
            Dispatcher.Dispatch(async () =>
            {
                await ShowActionsButton.ScaleToAsync(1.25, 200, Easing.CubicOut);
                await ShowActionsButton.ScaleToAsync(1.0, 200, Easing.CubicIn);
            });
        }

        HideSliders();

        BrushSizeButton.IsVisible = false;
        AlphaButton.IsVisible = false;
        ColorPaletteButton.IsVisible = false;
        BoundingBoxSizeButton.IsVisible = false;
        AddRemoveButton.IsVisible = false;
        ResetZoomButton.IsVisible = false;
        NoiseButton.IsVisible = false;
        MaskPaddingButton.IsVisible = false;

        foreach (var contextButton in CurrentTool.ContextButtons)
        {
            switch (contextButton)
            {
                case ContextButtonType.BrushSize:
                    BrushSizeButton.IsVisible = true;
                    break;
                case ContextButtonType.Alpha:
                    AlphaButton.IsVisible = true;
                    break;
                case ContextButtonType.ColorPicker:
                    ColorPaletteButton.IsVisible = true;
                    break;
                case ContextButtonType.BoundingBoxSize:
                    BoundingBoxSizeButton.IsVisible = true;
                    break;
                case ContextButtonType.AddRemove:
                    AddRemoveButton.IsVisible = true;
                    break;
                case ContextButtonType.ResetZoom:
                    ResetZoomButton.IsVisible = true;
                    break;
                case ContextButtonType.Noise:
                    NoiseButton.IsVisible = true;
                    break;
                case ContextButtonType.MaskPadding:
                    MaskPaddingButton.IsVisible = true;
                    break;
                default:
                    break;
            }
        }

        updateTextFlipButtonsVisibility();
    }

    private void disposeTimers()
    {
        _sliderTimer?.Dispose();
        _sliderTimer = null;
    }
}