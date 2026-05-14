using Mogri.Enums;

namespace Mogri.Views;

/// <summary>
/// Canvas page partial that manages slider chrome, action-tray animation, haptics, timers, and tool-context button visibility.
/// </summary>
public partial class CanvasPage
{
    // Auto-hide UI chrome.
    private Timer? _brushSizeTimer;
    private Timer? _alphaTimer;
    private Timer? _noiseTimer;

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
            double translation = (ActionsContainer.Height / 4);
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
        vibrate(HapticFeedbackType.Click);

        ShowHideAlphaSlider(false);
        ShowHideBrushSizeSlider(!BrushSizeSliderContainer.IsVisible);
        ShowHideNoiseSlider(false);
    }

    private void Alpha_Button_Clicked(object? sender, EventArgs e)
    {
        vibrate(HapticFeedbackType.Click);

        ShowHideBrushSizeSlider(false);
        ShowHideAlphaSlider(!AlphaSliderContainer.IsVisible);
        ShowHideNoiseSlider(false);
    }

    private void Noise_Button_Clicked(object? sender, EventArgs e)
    {
        vibrate(HapticFeedbackType.Click);

        ShowHideBrushSizeSlider(false);
        ShowHideAlphaSlider(false);
        ShowHideNoiseSlider(!NoiseSliderContainer.IsVisible);
    }

    private void ShowHideAlphaSlider(bool show)
    {
        if (show)
        {
            AlphaSliderContainer.Opacity = 0f;
            AlphaSliderContainer.IsVisible = true;
        }

        AlphaSliderContainer.AbortAnimation("FadeInOutAlpha");
        AlphaSliderContainer.Animate("FadeInOutAlpha", value =>
        {
            AlphaSliderContainer.Opacity = value;
        }, AlphaSliderContainer.Opacity, show ? 1 : 0, easing: Easing.CubicInOut, finished: (value, canceled) =>
        {
            if (canceled)
            {
                return;
            }

            AlphaSliderContainer.IsVisible = show;

            if (AlphaSliderContainer.IsVisible)
            {
                AutoHideAlphaSlider();
            }
        });
    }

    private void ShowHideBrushSizeSlider(bool show)
    {
        if (show)
        {
            BrushSizeSliderContainer.Opacity = 0f;
            BrushSizeSliderContainer.IsVisible = true;
        }

        BrushSizeSliderContainer.AbortAnimation("FadeInOutBrushSize");
        BrushSizeSliderContainer.Animate("FadeInOutBrushSize", value =>
        {
            BrushSizeSliderContainer.Opacity = value;
        }, BrushSizeSliderContainer.Opacity, show ? 1 : 0, easing: Easing.CubicInOut, finished: (value, canceled) =>
        {
            if (canceled)
            {
                return;
            }

            BrushSizeSliderContainer.IsVisible = show;

            if (BrushSizeSliderContainer.IsVisible)
            {
                AutoHideBrushSizeSlider();
            }
        });
    }

    private void ShowHideNoiseSlider(bool show)
    {
        if (show)
        {
            NoiseSliderContainer.Opacity = 0f;
            NoiseSliderContainer.IsVisible = true;
        }

        NoiseSliderContainer.AbortAnimation("FadeInOutNoise");
        NoiseSliderContainer.Animate("FadeInOutNoise", value =>
        {
            NoiseSliderContainer.Opacity = value;
        }, NoiseSliderContainer.Opacity, show ? 1 : 0, easing: Easing.CubicInOut, finished: (value, canceled) =>
        {
            if (canceled)
            {
                return;
            }

            NoiseSliderContainer.IsVisible = show;

            if (NoiseSliderContainer.IsVisible)
            {
                AutoHideNoiseSlider();
            }
        });
    }

    private void AutoHideBrushSizeSlider()
    {
        if (_brushSizeTimer == null)
        {
            _brushSizeTimer = new Timer(delegate
            {
                Dispatcher.Dispatch(() =>
                {
                    ShowHideBrushSizeSlider(false);
                });
            }, null, 3000, -1);
        }
        else
        {
            _brushSizeTimer.Change(3000, -1);
        }
    }

    private void AutoHideAlphaSlider()
    {
        if (_alphaTimer == null)
        {
            _alphaTimer = new Timer(delegate
            {
                Dispatcher.Dispatch(() =>
                {
                    ShowHideAlphaSlider(false);
                });
            }, null, 3000, -1);
        }
        else
        {
            _alphaTimer.Change(3000, -1);
        }
    }

    private void AutoHideNoiseSlider()
    {
        if (_noiseTimer == null)
        {
            _noiseTimer = new Timer(delegate
            {
                Dispatcher.Dispatch(() =>
                {
                    ShowHideNoiseSlider(false);
                });
            }, null, 3000, -1);
        }
        else
        {
            _noiseTimer.Change(3000, -1);
        }
    }

    private void HideSliders()
    {
        AlphaSliderContainer.IsVisible = false;
        BrushSizeSliderContainer.IsVisible = false;
        NoiseSliderContainer.IsVisible = false;
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

        ShowHideBrushSizeSlider(false);
        ShowHideAlphaSlider(false);
        ShowHideNoiseSlider(false);

        BrushSizeButton.IsVisible = false;
        AlphaButton.IsVisible = false;
        ColorPaletteButton.IsVisible = false;
        BoundingBoxSizeButton.IsVisible = false;
        AddRemoveButton.IsVisible = false;
        ResetZoomButton.IsVisible = false;
        NoiseButton.IsVisible = false;

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
                default:
                    break;
            }
        }
    }

    private void disposeTimers()
    {
        _brushSizeTimer?.Dispose();
        _brushSizeTimer = null;

        _alphaTimer?.Dispose();
        _alphaTimer = null;

        _noiseTimer?.Dispose();
        _noiseTimer = null;
    }
}