using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.ViewModels;
using MobileDiffusion.ViewModels.CanvasContextButtons;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Views;

public partial class CanvasPage : BasePage
{
    private MaskLineViewModel _currentLine;
    private Timer _brushSizeTimer;
    private Timer _alphaTimer;
    private bool _hasCreatedBoundingBox;
    private bool _isSaving;
    private bool _hapticsEnabled = false;

    public SKBitmap Bitmap
    {
        get => (SKBitmap)GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
    }

    public SKBitmap SegmentationBitmap
    {
        get => (SKBitmap)GetValue(SegmentationBitmapProperty);
        set => SetValue(SegmentationBitmapProperty, value);
    }

    public float CurrentAlpha
    {
        get => (float)GetValue(CurrentAlphaProperty);
        set => SetValue(CurrentAlphaProperty, value);
    }

    public Color CurrentColor
    {
        get => (Color)GetValue(CurrentColorProperty);
        set => SetValue(CurrentColorProperty, value);
    }

    public float CurrentBrushSize
    {
        get => (float)GetValue(CurrentBrushSizeProperty);
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

    public IAsyncRelayCommand<SKBitmap> SegmentationCallbackCommand
    {
        get => (IAsyncRelayCommand<SKBitmap>)GetValue(SegmentationCallbackCommandProperty);
        set => SetValue(SegmentationCallbackCommandProperty, value);
    }

    public IAsyncRelayCommand<SKPoint> DoSegmentationCommand
    {
        get => (IAsyncRelayCommand<SKPoint>)GetValue(DoSegmentationCommandProperty);
        set => SetValue(DoSegmentationCommandProperty, value);
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

    public static BindableProperty CurrentBrushSizeProperty = BindableProperty.Create(nameof(CurrentBrushSize), typeof(float), typeof(CanvasPage), 10f, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).AutoHideBrushSizeSlider();
    });

    public static BindableProperty CurrentAlphaProperty = BindableProperty.Create(nameof(CurrentAlpha), typeof(float), typeof(CanvasPage), .5f, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).AutoHideAlphaSlider();
    });

    public static BindableProperty CurrentColorProperty = BindableProperty.Create(nameof(CurrentColor), typeof(Color), typeof(CanvasPage), Colors.Black);

    public static BindableProperty CurrentToolProperty = BindableProperty.Create(nameof(CurrentTool), typeof(IPaintingToolViewModel), typeof(CanvasPage), null, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnCurrentToolChanged();
    });

    public static BindableProperty BoundingBoxProperty = BindableProperty.Create(nameof(BoundingBox), typeof(SKRect), typeof(CanvasPage), default(SKRect));

    public static BindableProperty BoundingBoxScaleProperty = BindableProperty.Create(nameof(BoundingBoxScale), typeof(double), typeof(CanvasPage), 1d, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).UpdateBoundingBox(true);
    });

    public static BindableProperty BoundingBoxSizeProperty = BindableProperty.Create(nameof(BoundingBoxSize), typeof(float), typeof(CanvasPage), 0f, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).UpdateBoundingBox(true);
    });

    public static BindableProperty CanvasActionsProperty = BindableProperty.Create(nameof(CanvasActions), typeof(ObservableCollection<CanvasActionViewModel>), typeof(CanvasPage), default(ObservableCollection<CanvasActionViewModel>), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnCanvasActionsChanged();
    });

    public static BindableProperty PrepareForSavingCommandProperty = BindableProperty.Create(nameof(PrepareForSavingCommand), typeof(IAsyncRelayCommand), typeof(CanvasPage), default(IAsyncRelayCommand));
    
    public static BindableProperty SegmentationCallbackCommandProperty = BindableProperty.Create(nameof(SegmentationCallbackCommand), typeof(IAsyncRelayCommand<SKBitmap>), typeof(CanvasPage), default(IAsyncRelayCommand<SKBitmap>));

    public static BindableProperty DoSegmentationCommandProperty = BindableProperty.Create(nameof(DoSegmentationCommand), typeof(IAsyncRelayCommand<SKPoint>), typeof(CanvasPage), default(IAsyncRelayCommand<SKPoint>));

    public static BindableProperty ShowBoundingBoxProperty = BindableProperty.Create(nameof(ShowBoundingBox), typeof(bool), typeof(CanvasPage), false, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).UpdateBoundingBox(false);
    });

    public static BindableProperty ShowMaskLayerProperty = BindableProperty.Create(nameof(ShowMaskLayer), typeof(bool), typeof(CanvasPage), true, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).UpdateMaskLayer();
    });

    public CanvasPage()
    {
        InitializeComponent();

        this.SetBinding(BitmapProperty, nameof(ICanvasPageViewModel.SourceBitmap));
        this.SetBinding(CurrentAlphaProperty, nameof(ICanvasPageViewModel.CurrentAlpha));
        this.SetBinding(CurrentColorProperty, nameof(ICanvasPageViewModel.CurrentColor));
        this.SetBinding(CurrentToolProperty, nameof(ICanvasPageViewModel.CurrentTool));
        this.SetBinding(CanvasActionsProperty, nameof(ICanvasPageViewModel.CanvasActions), BindingMode.TwoWay);
        this.SetBinding(BoundingBoxProperty, nameof(ICanvasPageViewModel.BoundingBox), BindingMode.OneWayToSource);
        this.SetBinding(PrepareForSavingCommandProperty, nameof(ICanvasPageViewModel.PrepareForSavingCommand), BindingMode.OneWayToSource);
        this.SetBinding(BoundingBoxScaleProperty, nameof(ICanvasPageViewModel.BoundingBoxScale), BindingMode.OneWayToSource);
        this.SetBinding(BoundingBoxSizeProperty, nameof(ICanvasPageViewModel.BoundingBoxSize), BindingMode.TwoWay);
        this.SetBinding(ShowMaskLayerProperty, nameof(ICanvasPageViewModel.ShowMaskLayer), BindingMode.OneWay);
        this.SetBinding(DoSegmentationCommandProperty, nameof(ICanvasPageViewModel.DoSegmentationCommand), BindingMode.OneWay);
        this.SetBinding(SegmentationBitmapProperty, nameof(ICanvasPageViewModel.SegmentationBitmap), BindingMode.TwoWay);

        PrepareForSavingCommand = new AsyncRelayCommand<IAsyncRelayCommand>(PrepareForSaving);
        
        //SourceImageCanvasView.SizeChanged += SourceImageCanvasView_SizeChanged;
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

    private void SourceImageCanvasView_SizeChanged(object sender, EventArgs e)
    {
        UpdateCanvasSizes();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is ICanvasPageViewModel pageViewModel)
        {
            pageViewModel.SourceCanvasView = SourceImageCanvasView;
            pageViewModel.MaskCanvasView = MaskCanvasView;
        }
    }

    private void OnTouchMaskSurface(object sender, SKTouchEventArgs e)
    {
        HideSliders();
        if (e.InContact)
        {
            if (e.Location is SKPoint location && CurrentTool != null && 
                (CurrentTool.Type == ToolType.PaintBrush || CurrentTool.Type == ToolType.Eraser || CurrentTool.Type == ToolType.BoundingBox))
            {
                if (ShowBoundingBox && 
                    CurrentTool.Type == ToolType.BoundingBox &&
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

                    MaskCanvasView.InvalidateSurface();

                    e.Handled = true;

                    return;
                }

                if (_currentLine == null)
                {
                    _currentLine = new()
                    {
                        CanvasActionType = CanvasActionType.Mask,
                        Alpha = CurrentAlpha,
                        BrushSize = CurrentBrushSize,
                        Color = CurrentColor,
                        MaskEffect = CurrentTool?.Effect ?? MaskEffect.Paint
                    };

                    CanvasActions ??= new();

                    CanvasActions.Add(_currentLine);
                }

                _currentLine.Path.Add(location);
            }
        }
        else if (e.Location is SKPoint location && CurrentTool != null && CurrentTool.Type == ToolType.PaintBucket)
        {
            _currentLine = null;

            var pixelPoint = new SKPoint(location.X * (float)BoundingBoxScale, location.Y * (float)BoundingBoxScale);

            DoSegmentationCommand?.Execute(pixelPoint);
        }
        else
        {
            _currentLine = null;
        }

        MaskCanvasView.InvalidateSurface();

        e.Handled = true;
    }

    private void OnPaintSourceImageSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (Bitmap != null)
        {
            //float scale = Math.Min((float)info.Width / Bitmap.Width,
            //           (float)info.Height / Bitmap.Height);
            //float x = (info.Width - scale * Bitmap.Width) / 2;
            //float y = (info.Height - scale * Bitmap.Height) / 2;
            //SKRect destRect = new SKRect(x, y, x + scale * Bitmap.Width,
            //                                   y + scale * Bitmap.Height);

            //canvas.DrawBitmap(Bitmap, destRect);

            canvas.DrawBitmap(Bitmap, Bitmap.Info.Rect, e.Info.Rect);
        }
    }

    private void OnPaintMaskSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (CanvasActions != null)
        {
            foreach (var canvasAction in CanvasActions.Where(ca => ca.CanvasActionType == CanvasActionType.Mask))
            {
                canvasAction.Execute(canvas, e.Info, _isSaving);
            }
        }

        if (!_isSaving && ShowBoundingBox)
        {
            canvas.DrawRect(BoundingBox,
            new SKPaint()
            {
                Color = SKColors.White,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
            });
        }
    }

    private void OnPaintSegmentationImageSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        
        if (SegmentationBitmap != null)
        {
            canvas.DrawBitmap(SegmentationBitmap, SegmentationBitmap.Info.Rect, e.Info.Rect);
        }
    }

    private void OnPaintOutlineSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        // Make sure the canvas is blank
        canvas.Clear(SKColors.Transparent);

        if (CanvasActions != null &&
            CanvasActions.Any())
        {
            using var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.None,
                IsAntialias = false,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 10,
                StrokeCap = SKStrokeCap.Round,
                StrokeMiter = 0,
                StrokeJoin = SKStrokeJoin.Round,
            };

            foreach (var canvasAction in CanvasActions)
            {
                if (canvasAction is MaskLineViewModel maskLine)
                {
                    if (maskLine.Alpha > .1f)
                    {
                        continue;
                    }

                    var points = maskLine.Path;

                    paint.StrokeWidth = maskLine.BrushSize;

                    paint.Color = new SKColor(
                        maskLine.Color.GetByteRed(),
                        maskLine.Color.GetByteGreen(),
                        maskLine.Color.GetByteBlue(),
                        maskLine.Color.GetByteAlpha());

                    using var path = new SKPath();
                    path.MoveTo(points[0]);

                    for (var i = 1; i < points.Count; i++)
                    {
                        path.ConicTo(points[i - 1], points[i], .5f);
                    }

                    canvas.DrawPath(path, paint);
                }
            }
        }
        
        if (ShowBoundingBox)
        {
            canvas.DrawRect(BoundingBox,
            new SKPaint()
            {
                Color = SKColors.White,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
            });
        }
    }

    private void Brush_Size_Button_Clicked(object sender, EventArgs e)
    {
        vibrate(HapticFeedbackType.Click);

        ShowHideAlphaSlider(false);
        ShowHideBrushSizeSlider(!BrushSizeSliderContainer.IsVisible);
    }

    private void Alpha_Button_Clicked(object sender, EventArgs e)
    {
        vibrate(HapticFeedbackType.Click);

        ShowHideBrushSizeSlider(false);
        ShowHideAlphaSlider(!AlphaSliderContainer.IsVisible);
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

    private void UpdateBoundingBox(bool sizeChanged)
    {        
        var rectSize = (float)(BoundingBoxSize / BoundingBoxScale);

        if (!_hasCreatedBoundingBox &&
            MaskCanvasView.Width != -1 &&
            MaskCanvasView.Height != -1)
        {
            BoundingBox = new SKRect(
                (float)(MaskCanvasView.Width / 2) - (rectSize / 2),
                (float)(MaskCanvasView.Height / 2) - (rectSize / 2),
                (float)(MaskCanvasView.Width / 2) + (rectSize / 2),
                (float)(MaskCanvasView.Height / 2) + (rectSize / 2));

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

        MaskCanvasView.InvalidateSurface();
    }

    private void HideSliders()
    {
        AlphaSliderContainer.IsVisible = false;
        BrushSizeSliderContainer.IsVisible = false;
    }

    private void Undo_Button_Clicked(object sender, EventArgs e)
    {
        HideSliders();

        if (CanvasActions == null || !CanvasActions.Any())
        {
            return;
        }

        CanvasActions.Remove(CanvasActions.Last());

        MaskCanvasView.InvalidateSurface();
    }

    private async void Clear_Button_Clicked(object sender, EventArgs e)
    {
        var result = await confirmClear();

        if (!result)
        {
            return;
        }

        HideSliders();

        if (CanvasActions == null || !CanvasActions.Any())
        {
            return;
        }

        CanvasActions.Clear();

        MaskCanvasView.InvalidateSurface();
    }

    private async Task<bool> confirmClear()
    {
        return await DisplayAlert("Clear mask?", "Are you sure you would like to clear the mask?", "YES", "Cancel");
    }

    private void OnCanvasActionsChanged()
    {
        MaskCanvasView.InvalidateSurface();
    }

    private void OnSourceBitmapChanged()
    {
        SegmentationBitmap = null;

        UpdateCanvasSizes();
    }

    private void UpdateCanvasSizes()
    {
        if (Bitmap == null)
        {
            return;
        }

        var scale = Math.Min((float)MaskGrid.Width / Bitmap.Width,(float)MaskGrid.Height / Bitmap.Height);
        var width = scale * Bitmap.Width;
        var height = scale * Bitmap.Height;

        SourceImageCanvasView.WidthRequest = width;
        SourceImageCanvasView.HeightRequest = height;
        
        MaskCanvasView.WidthRequest = width;
        MaskCanvasView.HeightRequest = height;

        SegmentationMaskCanvasView.WidthRequest = width;
        SegmentationMaskCanvasView.HeightRequest = height;

        // Clear lines
        //Clear_Button_Clicked(this, new EventArgs());

        // Force a measure on both canvas views because setting width/height request doesn't seem to be enough
        SourceImageCanvasView.Measure(width, height);
        SourceImageCanvasView.InvalidateSurface();
        MaskCanvasView.Measure(width, height);
        MaskCanvasView.InvalidateSurface();
        SegmentationMaskCanvasView.Measure(width, height);
        SegmentationMaskCanvasView.InvalidateSurface();

        BoundingBoxScale = Bitmap.Width / width;
    }

    private async Task PrepareForSaving(IAsyncRelayCommand callbackCommand)
    {
        if (callbackCommand == null)
        {
            return;
        }

        _isSaving = true;

        MaskCanvasView.InvalidateSurface();

        // Wait for canvas to redraw - hack - find a better solution (maybe the PaintSurface event?)
        await Task.Delay(300);

        await callbackCommand.ExecuteAsync(this);

        _isSaving = false;

        MaskCanvasView.InvalidateSurface();
    }

    private void MaskGrid_SizeChanged(object sender, EventArgs e)
    {
        UpdateCanvasSizes();
    }

    private void UpdateMaskLayer()
    {
        MaskCanvasView.AbortAnimation("FadeInOutMaskCanvasView");
        MaskCanvasView.Animate("FadeInOutMaskCanvasView", value => MaskCanvasView.Opacity = value, MaskCanvasView.Opacity, ShowMaskLayer ? 1 : 0, easing: Easing.CubicInOut);
    }

    private void ToolCollectionView_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
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

    private void Vibrate_Button_Tapped(object sender, TappedEventArgs e)
    {
        vibrate(HapticFeedbackType.Click);
    }

    private void OnSegmentationBitmapChanged()
    {
        SegmentationMaskCanvasView.InvalidateSurface();
    }

    private void OnCurrentToolChanged()
    {
        if (CurrentTool?.ContextButtons == null)
        {
            ShowBoundingBox = false;
            return;
        }

        ShowBoundingBox = CurrentTool.Type == ToolType.BoundingBox;

        BrushSizeButton.IsVisible = false;
        AlphaButton.IsVisible = false;
        ColorPaletteButton.IsVisible = false;
        SnipButton.IsVisible = false;
        BoundingBoxSizeButton.IsVisible = false;

        foreach (var contextButton in CurrentTool.ContextButtons)
        {
            switch (contextButton)
            {
                case BrushSizeContextButtonViewModel:
                    BrushSizeButton.IsVisible = true;
                    break;
                case AlphaContextButtonViewModel:
                    AlphaButton.IsVisible = true;
                    break;
                case ColorPickerContextButtonViewModel:
                    ColorPaletteButton.IsVisible = true;
                    break;
                case SnipContextButtonViewModel:
                    SnipButton.IsVisible = true;
                    break;
                case BoundingBoxSizeContextButtonViewModel:
                    BoundingBoxSizeButton.IsVisible = true;
                    break;
                default:
                    break;
            }
        }
    }
}

