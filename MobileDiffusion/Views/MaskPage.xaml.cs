using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace MobileDiffusion.Views;

public partial class MaskPage : ContentPage
{
    private MaskLine _currentLine;
    private Timer _brushSizeTimer;
    private Timer _alphaTimer;
    private bool _hasShownInitImgRectangle;

    public SKBitmap Bitmap
    {
        get => (SKBitmap)GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
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

    public List<MaskLine> Lines
    {
        get => (List<MaskLine>)GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    public SKRect InitImgRectangle
    {
        get => (SKRect)GetValue(InitImgRectangleProperty);
        set => SetValue(InitImgRectangleProperty, value);
    }

    public bool ShowInitImgRectangle
    {
        get => (bool)GetValue(ShowInitImgRectangleProperty);
        set => SetValue(ShowInitImgRectangleProperty, value);
    }

    public IAsyncRelayCommand PrepareForSavingCommand
    {
        get => (IAsyncRelayCommand)GetValue(PrepareForSavingCommandProperty);
        set => SetValue(PrepareForSavingCommandProperty, value);
    }

    public IAsyncRelayCommand FinishSavingCommand
    {
        get => (IAsyncRelayCommand)GetValue(FinishSavingCommandProperty);
        set => SetValue(FinishSavingCommandProperty, value);
    }

    public static BindableProperty BitmapProperty = BindableProperty.Create(nameof(Bitmap), typeof(SKBitmap), typeof(MaskPage), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((MaskPage)bindable).OnSourceBitmapChanged();
    });

    public static BindableProperty CurrentBrushSizeProperty = BindableProperty.Create(nameof(CurrentBrushSize), typeof(float), typeof(MaskPage), 10f, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((MaskPage)bindable).AutoHideBrushSizeSlider();
    });

    public static BindableProperty CurrentAlphaProperty = BindableProperty.Create(nameof(CurrentAlpha), typeof(float), typeof(MaskPage), .5f, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((MaskPage)bindable).AutoHideAlphaSlider();
    });

    public static BindableProperty CurrentColorProperty = BindableProperty.Create(nameof(CurrentColor), typeof(Color), typeof(MaskPage), Colors.Black);

    public static BindableProperty LinesProperty = BindableProperty.Create(nameof(Lines), typeof(List<MaskLine>), typeof(MaskPage), default(List<MaskLine>));

    public static BindableProperty InitImgRectangleProperty = BindableProperty.Create(nameof(InitImgRectangle), typeof(SKRect), typeof(MaskPage), default(SKRect));

    public static BindableProperty FinishSavingCommandProperty = BindableProperty.Create(nameof(FinishSavingCommand), typeof(IAsyncRelayCommand), typeof(MaskPage), default(IAsyncRelayCommand));

    public static BindableProperty PrepareForSavingCommandProperty = BindableProperty.Create(nameof(PrepareForSavingCommand), typeof(IAsyncRelayCommand), typeof(MaskPage), default(IAsyncRelayCommand));

    public static BindableProperty ShowInitImgRectangleProperty = BindableProperty.Create(nameof(ShowInitImgRectangle), typeof(bool), typeof(MaskPage), false, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((MaskPage)bindable).OnShowInitImgRectangleChanged();
    });

    public MaskPage()
	{
		InitializeComponent();

        this.SetBinding(BitmapProperty, nameof(IMaskPageViewModel.SourceBitmap));
        this.SetBinding(CurrentColorProperty, nameof(IMaskPageViewModel.CurrentColor));
        this.SetBinding(LinesProperty, nameof(IMaskPageViewModel.Lines));
        this.SetBinding(InitImgRectangleProperty, nameof(IMaskPageViewModel.InitImgRectangle), BindingMode.OneWayToSource);
        this.SetBinding(ShowInitImgRectangleProperty, nameof(IMaskPageViewModel.ShowInitImgRectangle));
        this.SetBinding(PrepareForSavingCommandProperty, nameof(IMaskPageViewModel.PrepareForSavingCommand), BindingMode.OneWayToSource);
        this.SetBinding(FinishSavingCommandProperty, nameof(IMaskPageViewModel.FinishSavingCommand));

        PrepareForSavingCommand = new AsyncRelayCommand(PrepareForSaving);
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is IMaskPageViewModel pageViewModel)
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
            if (e.Location is SKPoint location)
            {
                if (ShowInitImgRectangle &&
                    _currentLine == null &&
                    ((InitImgRectangle.Width > 0 &&
                     InitImgRectangle.Height > 0 &&
                     InitImgRectangle.Contains(location)) ||
                     e.ActionType == SKTouchAction.Moved))
                {
                    var offsetX = -(InitImgRectangle.Width / 2);
                    var offsetY = -(InitImgRectangle.Height / 2);

                    if (location.X + offsetX < 0)
                    {
                        offsetX = -location.X;
                    }
                    else if (location.X + offsetX + InitImgRectangle.Width > MaskCanvasView.Width)
                    {
                        offsetX = (float)MaskCanvasView.Width - location.X - InitImgRectangle.Width;
                    }

                    if (location.Y + offsetY < 0)
                    {
                        offsetY = -location.Y;
                    }
                    else if (location.Y + offsetY + InitImgRectangle.Height > MaskCanvasView.Height)
                    {
                        offsetY = (float)MaskCanvasView.Height - location.Y - InitImgRectangle.Height;
                    }

                    location.Offset(offsetX, offsetY);

                    InitImgRectangle = SKRect.Create(location, InitImgRectangle.Size);

                    MaskCanvasView.InvalidateSurface();

                    e.Handled = true;

                    return;
                }

                if (_currentLine == null)
                {
                    _currentLine = new()
                    {
                        Alpha = CurrentAlpha,
                        BrushSize = CurrentBrushSize,
                        Color = CurrentColor
                    };

                    Lines ??= new();

                    Lines.Add(_currentLine);
                }

                _currentLine.Path.Add(location);
            }
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
        var info = e.Info;

        // the the canvas and properties
        var canvas = e.Surface.Canvas;

        // make sure the canvas is blank
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
        // the the canvas and properties
        var canvas = e.Surface.Canvas;

        // make sure the canvas is blank
        canvas.Clear(SKColors.Transparent);

        if (Lines != null &&
            Lines.Any())
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

            foreach (var line in Lines)
            {
                var points = line.Path;

                paint.StrokeWidth = line.BrushSize;
                paint.Color = new SKColor(
                    line.Color.GetByteRed(),
                    line.Color.GetByteGreen(),
                    line.Color.GetByteBlue(),
                    Convert.ToByte((int)(line.Alpha * 255)));

                using var path = new SKPath();
                path.MoveTo(points[0]);

                for (var i = 1; i < points.Count; i++)
                {
                    path.ConicTo(points[i - 1], points[i], .5f);
                }

                canvas.DrawPath(path, paint);
            }
        }

        if (ShowInitImgRectangle)
        {
            canvas.DrawRect(InitImgRectangle,
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
        ShowHideAlphaSlider(false);
        ShowHideBrushSizeSlider(!BrushSizeSliderContainer.IsVisible);
    }

    private void Alpha_Button_Clicked(object sender, EventArgs e)
    {
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

    private void OnShowInitImgRectangleChanged()
    {
        if (Bitmap != null &&
            ShowInitImgRectangle &&
            !_hasShownInitImgRectangle)
        {
            InitImgRectangle = new SKRect(
                (float)(MaskCanvasView.Width / 2) - 50,
                (float)(MaskCanvasView.Height / 2) - 50,
                (float)(MaskCanvasView.Width / 2) + 50,
                (float)(MaskCanvasView.Height / 2) + 50);

            _hasShownInitImgRectangle = true;
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

        if (Lines == null || !Lines.Any())
        {
            return;
        }

        Lines.Remove(Lines.Last());

        MaskCanvasView.InvalidateSurface();
    }

    private void Clear_Button_Clicked(object sender, EventArgs e)
    {
        HideSliders();

        if (Lines == null || !Lines.Any())
        {
            return;
        }

        Lines.Clear();

        MaskCanvasView.InvalidateSurface();
    }

    private void OnSourceBitmapChanged()
    {
        _hasShownInitImgRectangle = false; 

        float scale = Math.Min((float)MaskGrid.Width / Bitmap.Width,
                   (float)MaskGrid.Height / Bitmap.Height);
        float x = ((float)MaskGrid.Width - scale * Bitmap.Width) / 2;
        float y = ((float)MaskGrid.Height - scale * Bitmap.Height) / 2;
        SKRect destRect = new SKRect(x, y, x + scale * Bitmap.Width,
                                           y + scale * Bitmap.Height);

        SourceImageCanvasView.WidthRequest = destRect.Width;
        SourceImageCanvasView.HeightRequest = destRect.Height;
        MaskCanvasView.WidthRequest = destRect.Width;
        MaskCanvasView.HeightRequest = destRect.Height;

        Clear_Button_Clicked(this, new EventArgs());

        SourceImageCanvasView.InvalidateSurface();
    }

    private async Task PrepareForSaving()
    {
        var rectangleWasShowing = ShowInitImgRectangle;

        if (rectangleWasShowing)
        {
            ShowInitImgRectangle = false;
        }

        await FinishSavingCommand.ExecuteAsync(this);

        if (rectangleWasShowing)
        {
            ShowInitImgRectangle = true;
        }
    }
}