using CommunityToolkit.Mvvm.Input;
using Mogri.Enums;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using Mogri.ViewModels;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using SkiaSharp.Views.Maui;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using Mogri.Models;

namespace Mogri.Views;

public partial class CanvasPage : BasePage
{
    // Text move-mode thresholds and selection chrome.
    private const float MinTextScale = 0.35f;
    private const float MaxTextScale = 6f;
    private const float TextSelectionPadding = 12f;
    private const float TextSelectionCornerRadius = 18f;
    private const float TextSelectionShadowStroke = 6f;
    private const float TextSelectionStroke = 3f;
    private const float DoubleTapThresholdMilliseconds = 350f;
    private const float MaxTapMovementInViewPixels = 12f;

    // Active canvas drawing state.
    private MaskLineViewModel? _currentLine;
    private MaskLineViewModel? _segmentationLine;

    // Auto-hide UI chrome.
    private Timer? _brushSizeTimer;
    private Timer? _alphaTimer;
    private Timer? _noiseTimer;

    // Page-level view state.
    private bool _hasCreatedBoundingBox;
    private bool _isSaving;
    private bool _hapticsEnabled = false;

    // Transient text selection and gesture state.
    private readonly CanvasPageTextInteractionState _textInteraction = new();

    public SKBitmap Bitmap
    {
        get => (SKBitmap)GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
    }

    public SKBitmap? SegmentationBitmap
    {
        get => (SKBitmap?)GetValue(SegmentationBitmapProperty);
        set => SetValue(SegmentationBitmapProperty, value);
    }

    public double CurrentAlpha
    {
        get => (double)GetValue(CurrentAlphaProperty);
        set => SetValue(CurrentAlphaProperty, value);
    }

    public Color CurrentColor
    {
        get => (Color)GetValue(CurrentColorProperty);
        set => SetValue(CurrentColorProperty, value);
    }

    public double CurrentBrushSize
    {
        get => (double)GetValue(CurrentBrushSizeProperty);
        set => SetValue(CurrentBrushSizeProperty, value);
    }

    public IPaintingToolViewModel CurrentTool
    {
        get => (IPaintingToolViewModel)GetValue(CurrentToolProperty);
        set => SetValue(CurrentToolProperty, value);
    }

    public ObservableCollection<CanvasActionViewModel> CanvasActions
    {
        get => (ObservableCollection<CanvasActionViewModel>)GetValue(CanvasActionsProperty);
        set => SetValue(CanvasActionsProperty, value);
    }

    public ObservableCollection<TextElementViewModel> TextElements
    {
        get => (ObservableCollection<TextElementViewModel>)GetValue(TextElementsProperty);
        set => SetValue(TextElementsProperty, value);
    }

    public bool TextAddMode
    {
        get => (bool)GetValue(TextAddModeProperty);
        set => SetValue(TextAddModeProperty, value);
    }

    public SKRect BoundingBox
    {
        get => (SKRect)GetValue(BoundingBoxProperty);
        set => SetValue(BoundingBoxProperty, value);
    }

    public bool ShowBoundingBox
    {
        get => (bool)GetValue(ShowBoundingBoxProperty);
        set => SetValue(ShowBoundingBoxProperty, value);
    }

    public bool ShowMaskLayer
    {
        get => (bool)GetValue(ShowMaskLayerProperty);
        set => SetValue(ShowMaskLayerProperty, value);
    }

    public IAsyncRelayCommand<IAsyncRelayCommand> PrepareForSavingCommand
    {
        get => (IAsyncRelayCommand<IAsyncRelayCommand>)GetValue(PrepareForSavingCommandProperty);
        set => SetValue(PrepareForSavingCommandProperty, value);
    }


    public IAsyncRelayCommand<SKPoint[]> DoSegmentationCommand
    {
        get => (IAsyncRelayCommand<SKPoint[]>)GetValue(DoSegmentationCommandProperty);
        set => SetValue(DoSegmentationCommandProperty, value);
    }

    public IRelayCommand ResetZoomCommand
    {
        get => (IRelayCommand)GetValue(ResetZoomCommandProperty);
        set => SetValue(ResetZoomCommandProperty, value);
    }

    public float BoundingBoxSize
    {
        get => (float)GetValue(BoundingBoxSizeProperty);
        set => SetValue(BoundingBoxSizeProperty, value);
    }

    public double BoundingBoxScale
    {
        get => (double)GetValue(BoundingBoxScaleProperty);
        set => SetValue(BoundingBoxScaleProperty, value);
    }

    public static BindableProperty BitmapProperty = BindableProperty.Create(nameof(Bitmap), typeof(SKBitmap), typeof(CanvasPage), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnSourceBitmapChanged();
    });

    public static BindableProperty SegmentationBitmapProperty = BindableProperty.Create(nameof(SegmentationBitmap), typeof(SKBitmap), typeof(CanvasPage), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnSegmentationBitmapChanged();
    });

    public static BindableProperty CurrentBrushSizeProperty = BindableProperty.Create(nameof(CurrentBrushSize), typeof(double), typeof(CanvasPage), 10d, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).AutoHideBrushSizeSlider();
    });

    public static BindableProperty CurrentAlphaProperty = BindableProperty.Create(nameof(CurrentAlpha), typeof(double), typeof(CanvasPage), .5d, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).AutoHideAlphaSlider();
    });

    public double CurrentNoise
    {
        get => (double)GetValue(CurrentNoiseProperty);
        set => SetValue(CurrentNoiseProperty, value);
    }

    public static BindableProperty CurrentNoiseProperty = BindableProperty.Create(nameof(CurrentNoise), typeof(double), typeof(CanvasPage), 0d, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).AutoHideNoiseSlider();
    });

    public static BindableProperty CurrentColorProperty = BindableProperty.Create(nameof(CurrentColor), typeof(Color), typeof(CanvasPage), Colors.Black);

    public static BindableProperty CurrentToolProperty = BindableProperty.Create(nameof(CurrentTool), typeof(IPaintingToolViewModel), typeof(CanvasPage), null, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnCurrentToolChanged();
    });

    public static BindableProperty BoundingBoxProperty = BindableProperty.Create(nameof(BoundingBox), typeof(SKRect), typeof(CanvasPage), default(SKRect));

    public static BindableProperty BoundingBoxScaleProperty = BindableProperty.Create(nameof(BoundingBoxScale), typeof(double), typeof(CanvasPage), 1d, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).UpdateBoundingBox(true, true);
    });

    public static BindableProperty BoundingBoxSizeProperty = BindableProperty.Create(nameof(BoundingBoxSize), typeof(float), typeof(CanvasPage), 0f, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).UpdateBoundingBox(true);
    });

    public static BindableProperty CanvasActionsProperty = BindableProperty.Create(nameof(CanvasActions), typeof(ObservableCollection<CanvasActionViewModel>), typeof(CanvasPage), default(ObservableCollection<CanvasActionViewModel>), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnCanvasActionsChanged(oldValue as ObservableCollection<CanvasActionViewModel>, newValue as ObservableCollection<CanvasActionViewModel>);
    });

    public static BindableProperty TextElementsProperty = BindableProperty.Create(nameof(TextElements), typeof(ObservableCollection<TextElementViewModel>), typeof(CanvasPage), default(ObservableCollection<TextElementViewModel>), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnTextElementsChanged(oldValue as ObservableCollection<TextElementViewModel>, newValue as ObservableCollection<TextElementViewModel>);
    });

    public static BindableProperty TextAddModeProperty = BindableProperty.Create(nameof(TextAddMode), typeof(bool), typeof(CanvasPage), true, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnTextAddModeChanged((bool)newValue);
    });

    public static BindableProperty PrepareForSavingCommandProperty = BindableProperty.Create(nameof(PrepareForSavingCommand), typeof(IAsyncRelayCommand), typeof(CanvasPage), default(IAsyncRelayCommand));


    public static BindableProperty DoSegmentationCommandProperty = BindableProperty.Create(nameof(DoSegmentationCommand), typeof(IAsyncRelayCommand<SKPoint[]>), typeof(CanvasPage), default(IAsyncRelayCommand<SKPoint[]>));

    public static BindableProperty ResetZoomCommandProperty = BindableProperty.Create(nameof(ResetZoomCommand), typeof(IRelayCommand), typeof(CanvasPage), default(IRelayCommand));

    public static BindableProperty ShowBoundingBoxProperty = BindableProperty.Create(nameof(ShowBoundingBox), typeof(bool), typeof(CanvasPage), false, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).UpdateBoundingBox(false);
    });

    public static BindableProperty ShowMaskLayerProperty = BindableProperty.Create(nameof(ShowMaskLayer), typeof(bool), typeof(CanvasPage), true, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).UpdateMaskLayer();
    });

    public static BindableProperty ShowActionsProperty = BindableProperty.Create(nameof(ShowActions), typeof(bool), typeof(CanvasPage), false, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).AnimateActionsContainer((bool)newValue);
    });

    public bool ShowActions
    {
        get => (bool)GetValue(ShowActionsProperty);
        set => SetValue(ShowActionsProperty, value);
    }

    public CanvasPage()
    {
        InitializeComponent();

        this.SetBinding(BitmapProperty, nameof(ICanvasPageViewModel.SourceBitmap));
        this.SetBinding(CurrentAlphaProperty, nameof(ICanvasPageViewModel.CurrentAlpha));
        this.SetBinding(CurrentBrushSizeProperty, nameof(ICanvasPageViewModel.CurrentBrushSize));
        this.SetBinding(CurrentNoiseProperty, nameof(ICanvasPageViewModel.CurrentNoise));
        this.SetBinding(CurrentColorProperty, nameof(ICanvasPageViewModel.CurrentColor), BindingMode.TwoWay);
        this.SetBinding(CurrentToolProperty, nameof(ICanvasPageViewModel.CurrentTool));
        this.SetBinding(CanvasActionsProperty, nameof(ICanvasPageViewModel.CanvasActions), BindingMode.TwoWay);
        this.SetBinding(TextElementsProperty, nameof(ICanvasPageViewModel.TextElements), BindingMode.TwoWay);
        this.SetBinding(TextAddModeProperty, nameof(ICanvasPageViewModel.TextAddMode), BindingMode.OneWay);
        this.SetBinding(BoundingBoxProperty, nameof(ICanvasPageViewModel.BoundingBox), BindingMode.OneWayToSource);
        this.SetBinding(PrepareForSavingCommandProperty, nameof(ICanvasPageViewModel.PrepareForSavingCommand), BindingMode.OneWayToSource);
        this.SetBinding(BoundingBoxScaleProperty, nameof(ICanvasPageViewModel.BoundingBoxScale), BindingMode.OneWayToSource);
        this.SetBinding(BoundingBoxSizeProperty, nameof(ICanvasPageViewModel.BoundingBoxSize), BindingMode.TwoWay);
        this.SetBinding(ShowMaskLayerProperty, nameof(ICanvasPageViewModel.ShowMaskLayer), BindingMode.TwoWay);
        this.SetBinding(DoSegmentationCommandProperty, nameof(ICanvasPageViewModel.DoSegmentationCommand), BindingMode.OneWay);
        this.SetBinding(SegmentationBitmapProperty, nameof(ICanvasPageViewModel.SegmentationBitmap), BindingMode.TwoWay);
        this.SetBinding(ShowActionsProperty, nameof(ICanvasPageViewModel.ShowActions), BindingMode.OneWay);
        this.SetBinding(ResetZoomCommandProperty, nameof(ICanvasPageViewModel.ResetZoomCommand), BindingMode.OneWayToSource);

        PrepareForSavingCommand = new AsyncRelayCommand<IAsyncRelayCommand>(PrepareForSaving);
        ResetZoomCommand = new RelayCommand(() => ZoomContainer.Reset(true));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        TemporaryCanvasView.SizeChanged += TemporaryCanvasView_SizeChanged;
        ActionsContainer.SizeChanged += ActionsContainer_SizeChanged;
    }

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

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        _ = Task.Run(async () =>
        {
            await Task.Delay(500);

            _hapticsEnabled = true;
        });
    }



    private void OnTouchTemporarySurface(object? sender, SKTouchEventArgs e)
    {
        HideSliders();

        if (e.Location is SKPoint location && CurrentTool != null)
        {
            float scale = 1f;
            var viewWidth = TemporaryCanvasView.CanvasSize.Width > 0 ? TemporaryCanvasView.CanvasSize.Width : MaskCanvasView.CanvasSize.Width;

            if (Bitmap != null && viewWidth > 0)
            {
                scale = (float)Bitmap.Width / viewWidth;
            }

            var imageLocation = new SKPoint(location.X * scale, location.Y * scale);

            if (CurrentTool.Type == ToolType.Text && !TextAddMode)
            {
                handleTextMoveModeTouch(e, location, imageLocation);
                TemporaryCanvasView.InvalidateSurface();
                e.Handled = true;
                return;
            }

            // InContact == Finger currently touching down
            if (e.InContact)
            {
                switch (CurrentTool.Type)
                {
                    case ToolType.BoundingBox:
                        if (ShowBoundingBox &&
                            BoundingBox.Width > 0 &&
                            BoundingBox.Height > 0 &&
                            BoundingBox.Contains(location))
                        {
                            var offsetX = -(BoundingBox.Width / 2);
                            var offsetY = -(BoundingBox.Height / 2);

                            if (location.X + offsetX < 0)
                            {
                                offsetX = -location.X;
                            }
                            else if (location.X + offsetX + BoundingBox.Width > MaskCanvasView.Width)
                            {
                                offsetX = (float)MaskCanvasView.Width - location.X - BoundingBox.Width;
                            }

                            if (location.Y + offsetY < 0)
                            {
                                offsetY = -location.Y;
                            }
                            else if (location.Y + offsetY + BoundingBox.Height > MaskCanvasView.Height)
                            {
                                offsetY = (float)MaskCanvasView.Height - location.Y - BoundingBox.Height;
                            }

                            location.Offset(offsetX, offsetY);

                            BoundingBox = SKRect.Create(location, BoundingBox.Size);
                        }

                        break;
                    case ToolType.Eyedropper:
                        if (Bitmap != null)
                        {
                            var x = (int)((location.X / TemporaryCanvasView.Width) * Bitmap.Width);
                            var y = (int)((location.Y / TemporaryCanvasView.Height) * Bitmap.Height);

                            if (x >= 0 && x < Bitmap.Width && y >= 0 && y < Bitmap.Height)
                            {
                                var pixelColor = Bitmap.GetPixel(x, y);
                                CurrentColor = pixelColor.ToMauiColor();
                            }
                        }
                        break;
                    case ToolType.PaintBrush:
                    case ToolType.Eraser:
                        ShowMaskLayer = true;

                        if (_currentLine == null)
                        {
                            _currentLine = new()
                            {
                                CanvasActionType = CanvasActionType.Mask,
                                Alpha = (float)CurrentAlpha,
                                BrushSize = (float)CurrentBrushSize * scale,
                                TouchScale = scale,
                                Color = CurrentColor,
                                Noise = CurrentNoise,
                                MaskEffect = CurrentTool?.Effect ?? MaskEffect.Paint
                            };
                        }

                        _currentLine.Path.Add(imageLocation);

                        if (_currentLine.MaskEffect == MaskEffect.Erase)
                        {
                            MaskCanvasView.InvalidateSurface();
                        }

                        break;
                    case ToolType.MagicWand:
                        ShowMaskLayer = true;

                        _segmentationLine ??= new()
                        {
                            CanvasActionType = CanvasActionType.Mask,
                            Alpha = .75f,
                            BrushSize = 10f * scale,
                            TouchScale = scale,
                            Color = Colors.White,
                            Noise = CurrentNoise,
                            MaskEffect = MaskEffect.Paint
                        };

                        _segmentationLine.Path.Add(imageLocation);

                        break;

                }
            }
            else
            {
                // Touch/click has been released

                if (_currentLine != null)
                {
                    _currentLine.Order = GetNextCanvasOrder();
                    CanvasActions?.Add(_currentLine);
                    _currentLine = null;

                    MaskCanvasView.InvalidateSurface();
                }

                switch (CurrentTool.Type)
                {
                    case ToolType.MagicWand:
                        if (_segmentationLine != null &&
                            _segmentationLine.Path.Count > 1)
                        {
                            var left = _segmentationLine.Path.Min(p => p.X);
                            var right = _segmentationLine.Path.Max(p => p.X);
                            var top = _segmentationLine.Path.Min(p => p.Y);
                            var bottom = _segmentationLine.Path.Max(p => p.Y);

                            var bounds = new SKRect(left, top, right, bottom);

                            if (bounds.Size.Width < (10 * scale) &&
                                bounds.Size.Height < (10 * scale))
                            {
                                DoSegmentationCommand?.Execute([imageLocation]);
                            }
                            else
                            {
                                var topLeft = new SKPoint(left, top);
                                var bottomRight = new SKPoint(right, bottom);

                                DoSegmentationCommand?.Execute([topLeft, bottomRight]);
                            }
                        }
                        else
                        {
                            DoSegmentationCommand?.Execute([imageLocation]);
                        }

                        _segmentationLine = null;
                        break;
                    case ToolType.Text when TextAddMode:
                        _ = PlaceTextAtPointAsync(imageLocation);
                        break;
                }
            }
        }

        TemporaryCanvasView.InvalidateSurface();

        e.Handled = true;
    }

    // Text move-mode interaction flow.
    private void handleTextMoveModeTouch(SKTouchEventArgs e, SKPoint viewLocation, SKPoint imageLocation)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _textInteraction.ActiveTouches[e.Id] = imageLocation;
                _textInteraction.ActiveTouchStartViewPoints[e.Id] = viewLocation;

                if (_textInteraction.ActiveTouches.Count == 1)
                {
                    var hitTextElement = getHitTextElement(imageLocation);
                    setSelectedTextElement(hitTextElement);

                    if (hitTextElement != null)
                    {
                        beginTextDragGesture(e.Id, imageLocation, hitTextElement);
                    }
                    else
                    {
                        _textInteraction.PrimaryTouchId = null;
                    }
                }
                else if (_textInteraction.SelectedTextElement != null && _textInteraction.ActiveTouches.Count >= 2)
                {
                    beginTextTransformGesture();
                }

                break;
            case SKTouchAction.Moved:
                if (_textInteraction.ActiveTouches.ContainsKey(e.Id))
                {
                    _textInteraction.ActiveTouches[e.Id] = imageLocation;
                }

                if (_textInteraction.SelectedTextElement == null)
                {
                    break;
                }

                if (_textInteraction.ActiveTouches.Count >= 2)
                {
                    if (!_textInteraction.IsTransformGesture)
                    {
                        beginTextTransformGesture();
                    }

                    updateTextTransformGesture();
                }
                else if (!_textInteraction.SuppressSingleTouchUntilRelease
                    && !_textInteraction.IsTransformGesture
                    && _textInteraction.PrimaryTouchId == e.Id)
                {
                    updateTextDragGesture(imageLocation);
                }

                break;
            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
            case SKTouchAction.Exited:
                completeTextMoveModeTouch(e.Id, viewLocation, imageLocation, e.ActionType == SKTouchAction.Released);
                break;
        }
    }

    private void beginTextDragGesture(long touchId, SKPoint imageLocation, TextElementViewModel textElement)
    {
        _textInteraction.PrimaryTouchId = touchId;
        _textInteraction.DragGestureStartTouchPoint = imageLocation;
        _textInteraction.DragGestureStartElementCenter = new SKPoint(textElement.X, textElement.Y);
        _textInteraction.IsTransformGesture = false;
        _textInteraction.SuppressSingleTouchUntilRelease = false;
    }

    private void updateTextDragGesture(SKPoint imageLocation)
    {
        var selectedTextElement = _textInteraction.SelectedTextElement;
        if (selectedTextElement == null)
        {
            return;
        }

        var deltaX = imageLocation.X - _textInteraction.DragGestureStartTouchPoint.X;
        var deltaY = imageLocation.Y - _textInteraction.DragGestureStartTouchPoint.Y;

        selectedTextElement.X = _textInteraction.DragGestureStartElementCenter.X + deltaX;
        selectedTextElement.Y = _textInteraction.DragGestureStartElementCenter.Y + deltaY;
    }

    private void beginTextTransformGesture()
    {
        var selectedTextElement = _textInteraction.SelectedTextElement;
        if (selectedTextElement == null || !tryGetActiveTransformPoints(out var firstPoint, out var secondPoint))
        {
            return;
        }

        _textInteraction.TransformGestureStartDistance = Math.Max(1f, getPointDistance(firstPoint, secondPoint));
        _textInteraction.TransformGestureStartAngle = getAngleDegrees(firstPoint, secondPoint);
        _textInteraction.TransformGestureStartScale = selectedTextElement.Scale;
        _textInteraction.TransformGestureStartRotation = selectedTextElement.Rotation;
        _textInteraction.IsTransformGesture = true;
        _textInteraction.PrimaryTouchId = null;
    }

    private void updateTextTransformGesture()
    {
        var selectedTextElement = _textInteraction.SelectedTextElement;
        if (selectedTextElement == null || !tryGetActiveTransformPoints(out var firstPoint, out var secondPoint))
        {
            return;
        }

        var currentDistance = Math.Max(1f, getPointDistance(firstPoint, secondPoint));
        var scaleFactor = currentDistance / _textInteraction.TransformGestureStartDistance;
        var currentAngle = getAngleDegrees(firstPoint, secondPoint);
        var angleDelta = normalizeDegrees(currentAngle - _textInteraction.TransformGestureStartAngle);

        selectedTextElement.Scale = Math.Clamp(_textInteraction.TransformGestureStartScale * scaleFactor, MinTextScale, MaxTextScale);
        selectedTextElement.Rotation = normalizeDegrees(_textInteraction.TransformGestureStartRotation + angleDelta);
    }

    private void completeTextMoveModeTouch(long touchId, SKPoint viewLocation, SKPoint imageLocation, bool isRelease)
    {
        var hadMultipleTouches = _textInteraction.ActiveTouches.Count > 1;
        var wasTransformGesture = _textInteraction.IsTransformGesture;
        var isTapCandidate = isRelease
            && !hadMultipleTouches
            && !wasTransformGesture
            && !_textInteraction.SuppressSingleTouchUntilRelease
            && isTapGesture(touchId, viewLocation);
        var tappedTextElement = isTapCandidate ? getHitTextElement(imageLocation) : null;

        _textInteraction.ActiveTouches.Remove(touchId);
        _textInteraction.ActiveTouchStartViewPoints.Remove(touchId);

        if (_textInteraction.ActiveTouches.Count >= 2 && _textInteraction.SelectedTextElement != null)
        {
            beginTextTransformGesture();
        }
        else if (wasTransformGesture && _textInteraction.ActiveTouches.Count == 1)
        {
            _textInteraction.IsTransformGesture = false;
            _textInteraction.PrimaryTouchId = _textInteraction.ActiveTouches.Keys.First();
            _textInteraction.SuppressSingleTouchUntilRelease = true;
        }
        else if (_textInteraction.ActiveTouches.Count == 1 && !wasTransformGesture)
        {
            _textInteraction.PrimaryTouchId = _textInteraction.ActiveTouches.Keys.First();

            if (_textInteraction.SelectedTextElement != null && _textInteraction.PrimaryTouchId.HasValue)
            {
                _textInteraction.DragGestureStartTouchPoint = _textInteraction.ActiveTouches[_textInteraction.PrimaryTouchId.Value];
                _textInteraction.DragGestureStartElementCenter = new SKPoint(_textInteraction.SelectedTextElement.X, _textInteraction.SelectedTextElement.Y);
            }
        }
        else if (_textInteraction.ActiveTouches.Count == 0)
        {
            _textInteraction.PrimaryTouchId = null;
            _textInteraction.IsTransformGesture = false;
            _textInteraction.SuppressSingleTouchUntilRelease = false;
        }

        if (isTapCandidate)
        {
            handleTextTapGesture(tappedTextElement, viewLocation);
        }
        else if (_textInteraction.ActiveTouches.Count == 0)
        {
            clearLastTextTap();
        }
    }

    private void handleTextTapGesture(TextElementViewModel? tappedTextElement, SKPoint viewLocation)
    {
        if (tappedTextElement == null)
        {
            setSelectedTextElement(null);
            clearLastTextTap();
            return;
        }

        setSelectedTextElement(tappedTextElement);

        var now = DateTime.UtcNow;
        var isDoubleTap = _textInteraction.LastTapElementId == tappedTextElement.Id
            && (now - _textInteraction.LastTapTimestampUtc).TotalMilliseconds <= DoubleTapThresholdMilliseconds
            && getPointDistance(_textInteraction.LastTapViewLocation, viewLocation) <= MaxTapMovementInViewPixels;

        if (isDoubleTap)
        {
            clearLastTextTap();
            _ = editSelectedTextAsync(tappedTextElement);
            return;
        }

        _textInteraction.LastTapElementId = tappedTextElement.Id;
        _textInteraction.LastTapTimestampUtc = now;
        _textInteraction.LastTapViewLocation = viewLocation;
    }

    private async Task editSelectedTextAsync(TextElementViewModel textElement)
    {
        if (BindingContext is not ICanvasPageViewModel viewModel)
        {
            return;
        }

        resetTextInteractionState(clearSelection: false, clearTapState: true);
        await viewModel.EditTextCommand.ExecuteAsync(textElement);
    }

    private TextElementViewModel? getHitTextElement(SKPoint imageLocation)
    {
        if (TextElements == null)
        {
            return null;
        }

        foreach (var textElement in TextElements.OrderByDescending(textElement => textElement.Order))
        {
            if (string.IsNullOrWhiteSpace(textElement.Text))
            {
                continue;
            }

            var bounds = GetTextBoundsWithFallback(textElement.Text, textElement.BaseFontSize);
            if (bounds.IsEmpty)
            {
                continue;
            }

            var localPoint = getLocalTextPoint(imageLocation, textElement, bounds);
            var hitBounds = bounds;
            hitBounds.Inflate(TextSelectionPadding, TextSelectionPadding);

            if (hitBounds.Contains(localPoint))
            {
                return textElement;
            }
        }

        return null;
    }

    private static SKPoint getLocalTextPoint(SKPoint imageLocation, TextElementViewModel textElement, SKRect bounds)
    {
        var translatedPoint = new SKPoint(imageLocation.X - textElement.X, imageLocation.Y - textElement.Y);
        var rotationRadians = -textElement.Rotation * (MathF.PI / 180f);
        var cos = MathF.Cos(rotationRadians);
        var sin = MathF.Sin(rotationRadians);
        var rotatedPoint = new SKPoint(
            translatedPoint.X * cos - translatedPoint.Y * sin,
            translatedPoint.X * sin + translatedPoint.Y * cos);
        var safeScale = Math.Max(textElement.Scale, MinTextScale);
        var unscaledPoint = new SKPoint(rotatedPoint.X / safeScale, rotatedPoint.Y / safeScale);

        return new SKPoint(unscaledPoint.X + bounds.MidX, unscaledPoint.Y + bounds.MidY);
    }

    private bool isTapGesture(long touchId, SKPoint viewLocation)
    {
        if (!_textInteraction.ActiveTouchStartViewPoints.TryGetValue(touchId, out var startPoint))
        {
            return false;
        }

        return getPointDistance(startPoint, viewLocation) <= MaxTapMovementInViewPixels;
    }

    private bool tryGetActiveTransformPoints(out SKPoint firstPoint, out SKPoint secondPoint)
    {
        firstPoint = default;
        secondPoint = default;

        if (_textInteraction.ActiveTouches.Count < 2)
        {
            return false;
        }

        var activePoints = _textInteraction.ActiveTouches.Values.Take(2).ToArray();
        firstPoint = activePoints[0];
        secondPoint = activePoints[1];
        return true;
    }

    private void setSelectedTextElement(TextElementViewModel? textElement)
    {
        if (ReferenceEquals(_textInteraction.SelectedTextElement, textElement))
        {
            if (_textInteraction.SelectedTextElement != null && !_textInteraction.SelectedTextElement.IsSelected)
            {
                _textInteraction.SelectedTextElement.IsSelected = true;
            }

            return;
        }

        if (_textInteraction.SelectedTextElement != null)
        {
            _textInteraction.SelectedTextElement.IsSelected = false;
        }

        _textInteraction.SelectedTextElement = textElement;

        if (_textInteraction.SelectedTextElement != null)
        {
            _textInteraction.SelectedTextElement.IsSelected = true;
        }
    }

    private void resetTextInteractionState(bool clearSelection, bool clearTapState)
    {
        _textInteraction.ResetGestureState(clearTapState);

        if (clearSelection)
        {
            setSelectedTextElement(null);
        }

        TemporaryCanvasView.InvalidateSurface();
    }

    private void clearLastTextTap()
    {
        _textInteraction.ClearTapState();
    }

    private void OnTextAddModeChanged(bool textAddMode)
    {
        if (textAddMode)
        {
            resetTextInteractionState(clearSelection: true, clearTapState: true);
            return;
        }

        TemporaryCanvasView.InvalidateSurface();
    }

    // Text move-mode geometry helpers.
    private static float getPointDistance(SKPoint firstPoint, SKPoint secondPoint)
    {
        var deltaX = secondPoint.X - firstPoint.X;
        var deltaY = secondPoint.Y - firstPoint.Y;

        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static float getAngleDegrees(SKPoint firstPoint, SKPoint secondPoint)
    {
        return MathF.Atan2(secondPoint.Y - firstPoint.Y, secondPoint.X - firstPoint.X) * (180f / MathF.PI);
    }

    private static float normalizeDegrees(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }


    private void OnPaintSourceImageSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (Bitmap != null)
        {
            canvas.DrawBitmap(Bitmap, Bitmap.Info.Rect, e.Info.Rect);
        }
    }

    private void OnPaintMaskSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (Bitmap == null)
        {
            return;
        }

        // Calculate scale to transform Image Coords -> View Coords
        // e.Info.Width is ViewPixels. Bitmap.Width is ImagePixels.
        // Scale = View / Image.
        var scale = (float)e.Info.Width / Bitmap.Width;

        if (CanvasActions != null || TextElements != null)
        {
            var overlayItems = new List<(long Order, CanvasActionViewModel? CanvasAction, TextElementViewModel? TextElement)>();

            if (CanvasActions != null)
            {
                overlayItems.AddRange(CanvasActions
                    .Where(canvasAction => canvasAction.CanvasActionType == CanvasActionType.Mask)
                    .Select(canvasAction => (Order: (long)canvasAction.Order, CanvasAction: (CanvasActionViewModel?)canvasAction, TextElement: (TextElementViewModel?)null)));
            }

            if (TextElements != null)
            {
                overlayItems.AddRange(TextElements
                    .Select(textElement => (Order: textElement.Order, CanvasAction: (CanvasActionViewModel?)null, TextElement: (TextElementViewModel?)textElement)));
            }

            foreach (var overlayItem in overlayItems.OrderBy(overlayItem => overlayItem.Order))
            {
                if (overlayItem.CanvasAction != null)
                {
                    // Scale the canvas so all mask actions render in "Image Space".
                    // We pass the Source Bitmap's Info (Virtual Image Space) to the action.
                    canvas.Save();
                    canvas.Scale(scale);

                    var virtualInfo = new SKImageInfo(Bitmap.Width, Bitmap.Height, e.Info.ColorType, e.Info.AlphaType);
                    overlayItem.CanvasAction.Execute(canvas, virtualInfo, _isSaving);

                    canvas.Restore();
                }
                else if (overlayItem.TextElement != null)
                {
                    DrawTextElement(canvas, overlayItem.TextElement, scale);
                }
            }
        }

        if (_currentLine != null && _currentLine.MaskEffect == MaskEffect.Erase)
        {
            canvas.Save();
            canvas.Scale(scale);
            _currentLine.Execute(canvas, e.Info, _isSaving);
            canvas.Restore();
        }
    }

    private void OnPaintTemporarySurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        float scale = 1f;
        if (Bitmap != null)
        {
            scale = (float)e.Info.Width / Bitmap.Width;
        }

        if (_currentLine != null && _currentLine.MaskEffect == MaskEffect.Paint)
        {
            canvas.Save();
            canvas.Scale(scale);
            _currentLine.Execute(canvas, e.Info, _isSaving);
            canvas.Restore();
        }

        if (_segmentationLine != null)
        {
            canvas.Save();
            canvas.Scale(scale);
            _segmentationLine.Execute(canvas, e.Info, _isSaving);
            canvas.Restore();
        }

        drawSelectedTextOutline(canvas, scale);

        if (ShowBoundingBox)
        {
            canvas.DrawRect(BoundingBox,
            new SKPaint()
            {
                Color = SKColors.Black.WithAlpha((byte)15),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 6,
            });

            var boxPaint = new SKPaint()
            {
                Color = SKColors.White,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
            };

            canvas.DrawRect(BoundingBox, boxPaint);
        }
    }

    private void OnPaintSegmentationImageSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (SegmentationBitmap != null)
        {
            canvas.DrawBitmap(SegmentationBitmap, SegmentationBitmap.Info.Rect, e.Info.Rect);
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

    private void OnCanvasActionsChanged(ObservableCollection<CanvasActionViewModel>? oldValue, ObservableCollection<CanvasActionViewModel>? newValue)
    {
        if (oldValue != null)
        {
            oldValue.CollectionChanged -= CanvasActions_CollectionChanged;
            foreach (var item in oldValue)
            {
                item.PropertyChanged -= OnCanvasActionPropertyChanged;
            }
        }

        if (newValue != null)
        {
            newValue.CollectionChanged += CanvasActions_CollectionChanged;
            foreach (var item in newValue)
            {
                item.PropertyChanged += OnCanvasActionPropertyChanged;
            }
        }

        MaskCanvasView.InvalidateSurface();
    }

    private void CanvasActions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (CanvasActionViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnCanvasActionPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (CanvasActionViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnCanvasActionPropertyChanged;
            }
        }

        MaskCanvasView.InvalidateSurface();
    }

    private void OnCanvasActionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        MaskCanvasView.InvalidateSurface();
    }

    private void OnTextElementsChanged(ObservableCollection<TextElementViewModel>? oldValue, ObservableCollection<TextElementViewModel>? newValue)
    {
        if (oldValue != null)
        {
            oldValue.CollectionChanged -= TextElements_CollectionChanged;
            foreach (var item in oldValue)
            {
                item.PropertyChanged -= OnTextElementPropertyChanged;
            }
        }

        if (newValue != null)
        {
            newValue.CollectionChanged += TextElements_CollectionChanged;
            foreach (var item in newValue)
            {
                item.PropertyChanged += OnTextElementPropertyChanged;
            }
        }

        MaskCanvasView.InvalidateSurface();
        TemporaryCanvasView.InvalidateSurface();
    }

    private void TextElements_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (TextElementViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnTextElementPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (TextElementViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnTextElementPropertyChanged;
            }
        }

        if (_textInteraction.SelectedTextElement != null && (TextElements == null || !TextElements.Contains(_textInteraction.SelectedTextElement)))
        {
            resetTextInteractionState(clearSelection: true, clearTapState: true);
        }

        MaskCanvasView.InvalidateSurface();
        TemporaryCanvasView.InvalidateSurface();
    }

    private void OnTextElementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (isTextElementRenderProperty(e.PropertyName))
        {
            MaskCanvasView.InvalidateSurface();
        }

        if (sender is TextElementViewModel textElement && shouldInvalidateTemporaryCanvas(textElement, e.PropertyName))
        {
            TemporaryCanvasView.InvalidateSurface();
        }
    }

    private void drawSelectedTextOutline(SKCanvas canvas, float canvasScale)
    {
        var selectedTextElement = _textInteraction.SelectedTextElement;

        if (selectedTextElement == null
            || CurrentTool?.Type != ToolType.Text
            || TextAddMode
            || TextElements == null
            || !TextElements.Contains(selectedTextElement)
            || string.IsNullOrWhiteSpace(selectedTextElement.Text))
        {
            return;
        }

        var bounds = GetTextBoundsWithFallback(selectedTextElement.Text, selectedTextElement.BaseFontSize);
        if (bounds.IsEmpty)
        {
            return;
        }

        var selectionBounds = bounds;
        selectionBounds.Inflate(TextSelectionPadding, TextSelectionPadding);

        canvas.Save();

        if (canvasScale != 1f)
        {
            canvas.Scale(canvasScale);
        }

        canvas.Translate(selectedTextElement.X, selectedTextElement.Y);
        canvas.RotateDegrees(selectedTextElement.Rotation);
        canvas.Scale(selectedTextElement.Scale);
        canvas.Translate(-bounds.MidX, -bounds.MidY);

        using var shadowPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(96),
            IsAntialias = true,
            StrokeWidth = TextSelectionShadowStroke,
            Style = SKPaintStyle.Stroke
        };
        using var outlinePaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(235),
            IsAntialias = true,
            StrokeWidth = TextSelectionStroke,
            Style = SKPaintStyle.Stroke
        };

        canvas.DrawRoundRect(selectionBounds, TextSelectionCornerRadius, TextSelectionCornerRadius, shadowPaint);
        canvas.DrawRoundRect(selectionBounds, TextSelectionCornerRadius, TextSelectionCornerRadius, outlinePaint);

        canvas.Restore();
    }

    private void OnSourceBitmapChanged()
    {
        SegmentationBitmap = null;

        UpdateCanvasSizes();

        if (BindingContext is ICanvasPageViewModel vm && vm.PreserveZoomOnNextBitmapChange)
        {
            vm.PreserveZoomOnNextBitmapChange = false;
        }
        else
        {
            ZoomContainer.Reset();
        }
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

        MaskCanvasView.WidthRequest = width;
        MaskCanvasView.HeightRequest = height;

        SegmentationMaskCanvasView.WidthRequest = width;
        SegmentationMaskCanvasView.HeightRequest = height;

        TemporaryCanvasView.WidthRequest = width;
        TemporaryCanvasView.HeightRequest = height;

        // Force a measure on both canvas views because setting width/height request doesn't seem to be enough
        SourceImageCanvasView.Measure(width, height);
        SourceImageCanvasView.InvalidateSurface();
        MaskCanvasView.Measure(width, height);
        MaskCanvasView.InvalidateSurface();
        SegmentationMaskCanvasView.Measure(width, height);
        SegmentationMaskCanvasView.InvalidateSurface();
        TemporaryCanvasView.Measure(width, height);
        TemporaryCanvasView.InvalidateSurface();

        BoundingBoxScale = Bitmap.Width / width;
    }

    private static bool isTextElementRenderProperty(string? propertyName)
    {
        return propertyName is nameof(TextElementViewModel.Text)
            or nameof(TextElementViewModel.X)
            or nameof(TextElementViewModel.Y)
            or nameof(TextElementViewModel.Scale)
            or nameof(TextElementViewModel.Rotation)
            or nameof(TextElementViewModel.Color)
            or nameof(TextElementViewModel.Alpha)
            or nameof(TextElementViewModel.IsSelected);
    }

    private static bool shouldInvalidateTemporaryCanvas(TextElementViewModel textElement, string? propertyName)
    {
        return propertyName == nameof(TextElementViewModel.IsSelected)
            || (textElement.IsSelected && isTextElementRenderProperty(propertyName));
    }

    private SKRect GetTextBoundsWithFallback(string text, float baseFontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return SKRect.Empty;
        }

        var combinedBounds = SKRect.Empty;
        var hasBounds = false;

        ProcessTextRunsWithFallback(text, baseFontSize, SKColors.White, (_, _, _, _, runBounds, _) =>
        {
            if (!hasBounds)
            {
                combinedBounds = runBounds;
                hasBounds = true;
                return;
            }

            combinedBounds = unionRects(combinedBounds, runBounds);
        });

        return combinedBounds;
    }

    private void DrawTextWithFallback(SKCanvas canvas, string text, Color color, float alpha, float baseFontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var skColor = color.ToSKColor().WithAlpha((byte)Math.Clamp((int)Math.Round(alpha * 255f), 0, 255));

        ProcessTextRunsWithFallback(text, baseFontSize, skColor, (runText, font, paint, shaper, _, originX) =>
        {
            canvas.DrawShapedText(shaper, runText, originX, 0f, font, paint);
        });
    }

    private void DrawTextElement(SKCanvas canvas, TextElementViewModel textElement, float canvasScale = 1f)
    {
        if (string.IsNullOrEmpty(textElement.Text))
        {
            return;
        }

        var bounds = GetTextBoundsWithFallback(textElement.Text, textElement.BaseFontSize);

        canvas.Save();

        if (canvasScale != 1f)
        {
            canvas.Scale(canvasScale);
        }

        canvas.Translate(textElement.X, textElement.Y);
        canvas.RotateDegrees(textElement.Rotation);
        canvas.Scale(textElement.Scale);
        canvas.Translate(-bounds.MidX, -bounds.MidY);
        DrawTextWithFallback(canvas, textElement.Text, textElement.Color, textElement.Alpha, textElement.BaseFontSize);

        canvas.Restore();
    }

    private SKBitmap PrepareSourceBitmapWithText(SKBitmap sourceBitmap)
    {
        var info = new SKImageInfo(sourceBitmap.Width, sourceBitmap.Height, sourceBitmap.ColorType, sourceBitmap.AlphaType, sourceBitmap.ColorSpace);
        var preparedBitmap = new SKBitmap(info);

        using var canvas = new SKCanvas(preparedBitmap);
        canvas.DrawBitmap(sourceBitmap, 0, 0);

        foreach (var textElement in TextElements.OrderBy(textElement => textElement.Order))
        {
            DrawTextElement(canvas, textElement);
        }

        return preparedBitmap;
    }

    private void ProcessTextRunsWithFallback(
        string text,
        float baseFontSize,
        SKColor color,
        Action<string, SKFont, SKPaint, SKShaper, SKRect, float> processRun)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var textRuns = BuildTextRunsWithFallback(text);

        try
        {
            var currentX = 0f;

            foreach (var textRun in textRuns)
            {
                using var font = new SKFont(textRun.Typeface, baseFontSize)
                {
                    Edging = SKFontEdging.Antialias,
                    LinearMetrics = true,
                    Subpixel = true
                };
                using var paint = new SKPaint
                {
                    Color = color,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                using var shaper = new SKShaper(textRun.Typeface);

                var shapedText = shaper.Shape(textRun.Text, currentX, 0f, font);
                var glyphs = shapedText.Codepoints.Select(static codepoint => (ushort)codepoint).ToArray();
                var glyphWidths = new float[glyphs.Length];
                var glyphBounds = new SKRect[glyphs.Length];

                if (glyphs.Length > 0)
                {
                    font.GetGlyphWidths(glyphs, glyphWidths, glyphBounds, paint);
                }

                var runBounds = getRunBounds(font, shapedText.Points, glyphBounds, glyphWidths, currentX);
                processRun(textRun.Text, font, paint, shaper, runBounds, currentX);

                currentX += getRunAdvance(shapedText.Points, glyphWidths, currentX);
            }
        }
        finally
        {
            foreach (var textRun in textRuns)
            {
                textRun.Dispose();
            }
        }
    }

    private static List<TextRun> BuildTextRunsWithFallback(string text)
    {
        var textRuns = new List<TextRun>();
        if (string.IsNullOrEmpty(text))
        {
            return textRuns;
        }

        var primaryTypeface = SKTypeface.Default;
        var currentText = new StringBuilder();
        SKTypeface? currentTypeface = null;
        var currentOwnsTypeface = false;

        var textElementEnumerator = StringInfo.GetTextElementEnumerator(text);
        while (textElementEnumerator.MoveNext())
        {
            var textElement = textElementEnumerator.GetTextElement();
            var (typeface, ownsTypeface) = getTypefaceForTextElement(primaryTypeface, textElement);

            if (currentTypeface == null)
            {
                currentText.Append(textElement);
                currentTypeface = typeface;
                currentOwnsTypeface = ownsTypeface;
                continue;
            }

            if (areEquivalentTypefaces(currentTypeface, typeface))
            {
                currentText.Append(textElement);

                if (ownsTypeface && !ReferenceEquals(typeface, currentTypeface))
                {
                    typeface.Dispose();
                }

                continue;
            }

            textRuns.Add(new TextRun(currentText.ToString(), currentTypeface, currentOwnsTypeface));
            currentText.Clear();
            currentText.Append(textElement);
            currentTypeface = typeface;
            currentOwnsTypeface = ownsTypeface;
        }

        if (currentTypeface != null && currentText.Length > 0)
        {
            textRuns.Add(new TextRun(currentText.ToString(), currentTypeface, currentOwnsTypeface));
        }

        return textRuns;
    }

    private static (SKTypeface Typeface, bool OwnsTypeface) getTypefaceForTextElement(SKTypeface primaryTypeface, string textElement)
    {
        using var primaryFont = new SKFont(primaryTypeface, 12f);
        if (primaryFont.ContainsGlyphs(textElement))
        {
            return (primaryTypeface, false);
        }

        var firstCodePoint = Rune.GetRuneAt(textElement, 0).Value;
        var fallbackTypeface = SKFontManager.Default.MatchCharacter(primaryTypeface.FamilyName, primaryTypeface.FontStyle, Array.Empty<string>(), firstCodePoint);

        if (fallbackTypeface == null)
        {
            return (primaryTypeface, false);
        }

        return (fallbackTypeface, !ReferenceEquals(fallbackTypeface, primaryTypeface));
    }

    private static bool areEquivalentTypefaces(SKTypeface left, SKTypeface right)
    {
        return left.FamilyName == right.FamilyName && left.FontStyle == right.FontStyle;
    }

    private static SKRect getRunBounds(SKFont font, SKPoint[] glyphPoints, SKRect[] glyphBounds, float[] glyphWidths, float originX)
    {
        var hasBounds = false;
        var runBounds = SKRect.Empty;

        for (var i = 0; i < glyphBounds.Length; i++)
        {
            var positionedBounds = glyphBounds[i];
            if (i < glyphPoints.Length)
            {
                positionedBounds.Offset(glyphPoints[i]);
            }
            else
            {
                positionedBounds.Offset(originX, 0f);
            }

            if (!hasBounds)
            {
                runBounds = positionedBounds;
                hasBounds = true;
                continue;
            }

            runBounds = unionRects(runBounds, positionedBounds);
        }

        if (hasBounds)
        {
            return runBounds;
        }

        var advance = getRunAdvance(glyphPoints, glyphWidths, originX);
        var metrics = font.Metrics;
        return new SKRect(originX, metrics.Ascent, originX + advance, metrics.Descent);
    }

    private static float getRunAdvance(SKPoint[] glyphPoints, float[] glyphWidths, float originX)
    {
        var rightMost = originX;

        for (var i = 0; i < glyphWidths.Length; i++)
        {
            var pointX = i < glyphPoints.Length ? glyphPoints[i].X : originX;
            var candidateRight = pointX + glyphWidths[i];
            if (candidateRight > rightMost)
            {
                rightMost = candidateRight;
            }
        }

        return Math.Max(0f, rightMost - originX);
    }

    private static SKRect unionRects(SKRect left, SKRect right)
    {
        return new SKRect(
            Math.Min(left.Left, right.Left),
            Math.Min(left.Top, right.Top),
            Math.Max(left.Right, right.Right),
            Math.Max(left.Bottom, right.Bottom));
    }

    private sealed class TextRun : IDisposable
    {
        private readonly bool _ownsTypeface;

        public TextRun(string text, SKTypeface typeface, bool ownsTypeface)
        {
            Text = text;
            Typeface = typeface;
            _ownsTypeface = ownsTypeface;
        }

        public string Text { get; }

        public SKTypeface Typeface { get; }

        public void Dispose()
        {
            if (_ownsTypeface)
            {
                Typeface.Dispose();
            }
        }
    }

    private async Task PrepareForSaving(IAsyncRelayCommand? callbackCommand)
    {
        if (callbackCommand == null)
        {
            return;
        }

        _isSaving = true;

        MaskCanvasView.InvalidateSurface();

        // Wait for canvas to redraw - hack - find a better solution (maybe the PaintSurface event?)
        await Task.Delay(300);

        var maskCapture = await MaskCanvasView.CaptureAsync();

        SKBitmap? maskBitmap = null;

        if (maskCapture != null)
        {
            using var maskStream = await maskCapture.OpenReadAsync();
            maskBitmap = SKBitmap.Decode(maskStream);
        }

        maskBitmap ??= new SKBitmap();

        var result = new CanvasCaptureResult
        {
            MaskBitmap = maskBitmap
        };

        if (Bitmap != null && TextElements is { Count: > 0 })
        {
            result.PreparedSourceBitmap = PrepareSourceBitmapWithText(Bitmap);
        }

        await callbackCommand.ExecuteAsync(result);

        _isSaving = false;

        MaskCanvasView.InvalidateSurface();
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

    private void OnSegmentationBitmapChanged()
    {
        SegmentationMaskCanvasView.InvalidateSurface();
    }

    private int GetNextCanvasOrder()
    {
        var nextCanvasActionOrder = CanvasActions?.Count > 0
            ? CanvasActions.Max(canvasAction => canvasAction.Order) + 1
            : 0;
        var nextTextOrder = TextElements?.Count > 0
            ? checked((int)(TextElements.Max(textElement => textElement.Order) + 1))
            : 0;

        return Math.Max(nextCanvasActionOrder, nextTextOrder);
    }

    private async Task PlaceTextAtPointAsync(SKPoint imageLocation)
    {
        if (BindingContext is ICanvasPageViewModel viewModel)
        {
            await viewModel.AddTextCommand.ExecuteAsync(imageLocation);
        }
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

            if (BindingContext is ICanvasPageViewModel viewModel && !viewModel.TextAddMode)
            {
                viewModel.TextAddMode = true;
            }
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
        TextModeButton.IsVisible = false;

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
                case ContextButtonType.TextMode:
                    TextModeButton.IsVisible = true;
                    break;
                default:
                    break;
            }
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        resetTextInteractionState(clearSelection: true, clearTapState: true);
        TemporaryCanvasView.SizeChanged -= TemporaryCanvasView_SizeChanged;
        ActionsContainer.SizeChanged -= ActionsContainer_SizeChanged;
        disposeTimers();
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
