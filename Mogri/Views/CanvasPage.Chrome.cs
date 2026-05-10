using Mogri.Enums;
using SkiaSharp;

namespace Mogri.Views;

public partial class CanvasPage
{
    // Auto-hide UI chrome.
    private Timer? _brushSizeTimer;
    private Timer? _alphaTimer;
    private Timer? _noiseTimer;

    // Page-level view state.
    private bool _hasCreatedBoundingBox;
    private bool _hapticsEnabled = false;

    private void ActionsContainer_SizeChanged(object? sender, EventArgs e)
    {
        if (ActionsContainer.Height < 0)
        {
            return;
        }

        AnimateActionsContainer(ShowActions, false);
    }

    private void TemporaryCanvasView_SizeChanged(object? sender, EventArgs e)
    {
        if (TemporaryCanvasView.Width != -1 &&
            TemporaryCanvasView.Height != -1)
        {
            UpdateBoundingBox(true, true);
        }
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

    private void UpdateBoundingBox(bool sizeChanged, bool resetPosition = false)
    {
        var rectSize = (float)(BoundingBoxSize / BoundingBoxScale);

        if ((!_hasCreatedBoundingBox || resetPosition) &&
            TemporaryCanvasView.Width != -1 &&
            TemporaryCanvasView.Height != -1)
        {
            BoundingBox = new SKRect(
                (float)(TemporaryCanvasView.Width / 2) - (rectSize / 2),
                (float)(TemporaryCanvasView.Height / 2) - (rectSize / 2),
                (float)(TemporaryCanvasView.Width / 2) + (rectSize / 2),
                (float)(TemporaryCanvasView.Height / 2) + (rectSize / 2));

            _hasCreatedBoundingBox = true;
        }
        else if (sizeChanged)
        {
            BoundingBox = new SKRect(
                BoundingBox.MidX - (rectSize / 2),
                BoundingBox.MidY - (rectSize / 2),
                BoundingBox.MidX + (rectSize / 2),
                BoundingBox.MidY + (rectSize / 2));
        }

        TemporaryCanvasView.InvalidateSurface();
    }

    private void HideSliders()
    {
        AlphaSliderContainer.IsVisible = false;
        BrushSizeSliderContainer.IsVisible = false;
        NoiseSliderContainer.IsVisible = false;
    }

    private void UpdateCanvasSizes()
    {
        if (Bitmap == null)
        {
            return;
        }

        var scale = Math.Min((float)MaskGrid.Width / Bitmap.Width, (float)MaskGrid.Height / Bitmap.Height);
        var width = scale * Bitmap.Width;
        var height = scale * Bitmap.Height;

        SourceImageCanvasView.WidthRequest = width;
        SourceImageCanvasView.HeightRequest = height;

        TextCanvasView.WidthRequest = width;
        TextCanvasView.HeightRequest = height;

        MaskCanvasView.WidthRequest = width;
        MaskCanvasView.HeightRequest = height;

        SegmentationMaskCanvasView.WidthRequest = width;
        SegmentationMaskCanvasView.HeightRequest = height;

        TemporaryCanvasView.WidthRequest = width;
        TemporaryCanvasView.HeightRequest = height;

        // Force a measure on both canvas views because setting width/height request doesn't seem to be enough
        SourceImageCanvasView.Measure(width, height);
        SourceImageCanvasView.InvalidateSurface();
        TextCanvasView.Measure(width, height);
        TextCanvasView.InvalidateSurface();
        MaskCanvasView.Measure(width, height);
        MaskCanvasView.InvalidateSurface();
        SegmentationMaskCanvasView.Measure(width, height);
        SegmentationMaskCanvasView.InvalidateSurface();
        TemporaryCanvasView.Measure(width, height);
        TemporaryCanvasView.InvalidateSurface();

        BoundingBoxScale = Bitmap.Width / width;
    }

    private void MaskGrid_SizeChanged(object? sender, EventArgs e)
    {
        UpdateCanvasSizes();
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