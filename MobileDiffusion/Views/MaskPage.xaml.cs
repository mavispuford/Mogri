using CommunityToolkit.Maui.Core.Extensions;
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

    public MaskPage()
	{
		InitializeComponent();

        this.SetBinding(BitmapProperty, nameof(IMaskPageViewModel.SourceBitmap));
        this.SetBinding(CurrentColorProperty, nameof(IMaskPageViewModel.CurrentColor));
        this.SetBinding(LinesProperty, nameof(IMaskPageViewModel.Lines));
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

        if (Lines == null || Lines.Count == 0)
        {
            return;
        }

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
}