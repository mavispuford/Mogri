using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MobileDiffusion.Models;

namespace MobileDiffusion.Views;

public partial class CanvasPage : BasePage
{
    private MaskLineViewModel _currentLine;
    private MaskLineViewModel _segmentationLine;
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

    public static BindableProperty PrepareForSavingCommandProperty = BindableProperty.Create(nameof(PrepareForSavingCommand), typeof(IAsyncRelayCommand), typeof(CanvasPage), default(IAsyncRelayCommand));
    
    public static BindableProperty SegmentationCallbackCommandProperty = BindableProperty.Create(nameof(SegmentationCallbackCommand), typeof(IAsyncRelayCommand<SKBitmap>), typeof(CanvasPage), default(IAsyncRelayCommand<SKBitmap>));

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
        this.SetBinding(CurrentColorProperty, nameof(ICanvasPageViewModel.CurrentColor), BindingMode.TwoWay);
        this.SetBinding(CurrentToolProperty, nameof(ICanvasPageViewModel.CurrentTool));
        this.SetBinding(CanvasActionsProperty, nameof(ICanvasPageViewModel.CanvasActions), BindingMode.TwoWay);
        this.SetBinding(BoundingBoxProperty, nameof(ICanvasPageViewModel.BoundingBox), BindingMode.OneWayToSource);
        this.SetBinding(PrepareForSavingCommandProperty, nameof(ICanvasPageViewModel.PrepareForSavingCommand), BindingMode.OneWayToSource);
        this.SetBinding(BoundingBoxScaleProperty, nameof(ICanvasPageViewModel.BoundingBoxScale), BindingMode.OneWayToSource);
        this.SetBinding(BoundingBoxSizeProperty, nameof(ICanvasPageViewModel.BoundingBoxSize), BindingMode.TwoWay);
        this.SetBinding(ShowMaskLayerProperty, nameof(ICanvasPageViewModel.ShowMaskLayer), BindingMode.OneWay);
        this.SetBinding(DoSegmentationCommandProperty, nameof(ICanvasPageViewModel.DoSegmentationCommand), BindingMode.OneWay);
        this.SetBinding(SegmentationBitmapProperty, nameof(ICanvasPageViewModel.SegmentationBitmap), BindingMode.TwoWay);
        this.SetBinding(ShowActionsProperty, nameof(ICanvasPageViewModel.ShowActions), BindingMode.OneWay);
        this.SetBinding(ResetZoomCommandProperty, nameof(ICanvasPageViewModel.ResetZoomCommand), BindingMode.OneWayToSource);

        PrepareForSavingCommand = new AsyncRelayCommand<IAsyncRelayCommand>(PrepareForSaving);
        ResetZoomCommand = new RelayCommand(() => ZoomContainer.Reset(true));

        TemporaryCanvasView.SizeChanged += TemporaryCanvasView_SizeChanged;
    }

    private void TemporaryCanvasView_SizeChanged(object sender, EventArgs e)
    {
        if (TemporaryCanvasView.Width != -1 &&
            TemporaryCanvasView.Height != -1)
        {
            UpdateBoundingBox(true, true);
        }
    }

    private async void AnimateActionsContainer(bool show)
    {
        if (show)
        {
            await ActionsContainer.TranslateToAsync(0, 0, 200, Easing.CubicInOut);
        }
        else
        {
            // Calculate height dynamically
            double translation = ActionsContainer.Height / 4;
            if (translation <= 0) translation = 200; // fallback if not measured
            
            await ActionsContainer.TranslateToAsync(0, translation, 200, Easing.CubicInOut);
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

    private void SourceImageCanvasView_SizeChanged(object sender, EventArgs e)
    {
        UpdateCanvasSizes();
    }


    private void OnTouchTemporarySurface(object sender, SKTouchEventArgs e)
    {
        HideSliders();

        if (e.Location is SKPoint location && CurrentTool != null)
        {
            float scale = 1f;
            if (Bitmap != null && TemporaryCanvasView.CanvasSize.Width > 0)
            {
                scale = (float)Bitmap.Width / TemporaryCanvasView.CanvasSize.Width;
            }

            var imageLocation = new SKPoint(location.X * scale, location.Y * scale);

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
                        if (_currentLine == null)
                        {
                            _currentLine = new()
                            {
                                CanvasActionType = CanvasActionType.Mask,
                                Alpha = (float)CurrentAlpha,
                                BrushSize = (float)CurrentBrushSize * scale,
                                Color = CurrentColor,
                                MaskEffect = CurrentTool?.Effect ?? MaskEffect.Paint
                            };
                        }

                        _currentLine.Path.Add(imageLocation);

                        if (_currentLine.MaskEffect == MaskEffect.Erase)
                        {
                            MaskCanvasView.InvalidateSurface();
                        }

                        break;
                    case ToolType.PaintBucket:
                        _segmentationLine ??= new()
                        {
                            CanvasActionType = CanvasActionType.Mask,
                            Alpha = .75f,
                            BrushSize = 10f * scale,
                            Color = Colors.White,
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
                    CanvasActions?.Add(_currentLine);
                    _currentLine = null;

                    MaskCanvasView.InvalidateSurface();
                }

                if (CurrentTool.Type == ToolType.PaintBucket)
                {
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
                }
            }
        }

        TemporaryCanvasView.InvalidateSurface();

        e.Handled = true;
    }

    private SKPoint getPixelPoint(SKPoint location) => new SKPoint(location.X * (float)BoundingBoxScale, location.Y * (float)BoundingBoxScale);

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
        
        // Calculate scale to transform Image Coords -> View Coords
        float scale = 1f;
        if (Bitmap != null) 
        {
            // e.Info.Width is ViewPixels. Bitmap.Width is ImagePixels.
            // Scale = View / Image.
            scale = (float)e.Info.Width / Bitmap.Width;
        }

        if (CanvasActions != null)
        {
            foreach (var canvasAction in CanvasActions.Where(ca => ca.CanvasActionType == CanvasActionType.Mask))
            {
                if (canvasAction is MaskLineViewModel)
                {
                    canvas.Save();
                    canvas.Scale(scale);
                    canvasAction.Execute(canvas, e.Info, _isSaving);
                    canvas.Restore();
                }
                else
                {
                    // SegmentationMaskViewModel uses Destination Rect logic internally or assumed View Space?
                    // SegmentationMaskViewModel.Execute draws to imageInfo.Rect (View Space).
                    // If the Bitmap is Image Size, it gets scaled down by DrawBitmap implicit scaling.
                    // So we DON't scale canvas for it.
                    canvasAction.Execute(canvas, e.Info, _isSaving);
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

    private void OnPaintTemporarySurface(object sender, SKPaintSurfaceEventArgs e)
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

    private void OnPaintSegmentationImageSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        
        if (SegmentationBitmap != null)
        {
            canvas.DrawBitmap(SegmentationBitmap, SegmentationBitmap.Info.Rect, e.Info.Rect);
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
    }

    private void OnCanvasActionsChanged(ObservableCollection<CanvasActionViewModel> oldValue, ObservableCollection<CanvasActionViewModel> newValue)
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

    private void CanvasActions_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
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

    private void OnCanvasActionPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        MaskCanvasView.InvalidateSurface();
    }

    private void OnSourceBitmapChanged()
    {
        SegmentationBitmap = null;

        UpdateCanvasSizes();

        ZoomContainer.Reset();
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

        var maskCapture = await MaskCanvasView.CaptureAsync();
        var result = new CanvasCaptureResult();

        if (maskCapture != null)
        {
            using var maskStream = await maskCapture.OpenReadAsync();
            result.MaskBitmap = SKBitmap.Decode(maskStream);
        }

        await callbackCommand.ExecuteAsync(result);

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

        if (ShowBoundingBox && !ShowActions)
        {
            Dispatcher.Dispatch(async () =>
            {
                await ShowActionsButton.ScaleTo(1.25, 200, Easing.CubicOut);
                await ShowActionsButton.ScaleTo(1.0, 200, Easing.CubicIn);
            });
        }

        BrushSizeButton.IsVisible = false;
        AlphaButton.IsVisible = false;
        ColorPaletteButton.IsVisible = false;
        BoundingBoxSizeButton.IsVisible = false;
        AddRemoveButton.IsVisible = false;
        ResetZoomButton.IsVisible = false;

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
                default:
                    break;
            }
        }
    }
}

