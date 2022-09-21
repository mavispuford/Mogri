using CommunityToolkit.Maui.Core.Extensions;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace MobileDiffusion.Views;

public partial class MaskPage : ContentPage
{
    private List<MaskLine> _lines = new();
    private MaskLine _currentLine;

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

    public static BindableProperty BitmapProperty = BindableProperty.Create(nameof(Bitmap), typeof(SKBitmap), typeof(MaskPage), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((MaskPage)bindable).OnSourceBitmapChanged();
    });

    public static BindableProperty CurrentBrushSizeProperty = BindableProperty.Create(nameof(CurrentBrushSize), typeof(float), typeof(MaskPage), 10f);


    public static BindableProperty CurrentAlphaProperty = BindableProperty.Create(nameof(CurrentAlpha), typeof(float), typeof(MaskPage), .5f);


    public static BindableProperty CurrentColorProperty = BindableProperty.Create(nameof(CurrentColor), typeof(Color), typeof(MaskPage), Colors.Black);

    public MaskPage()
	{
		InitializeComponent();

        this.SetBinding(BitmapProperty, nameof(IMaskPageViewModel.SourceBitmap));
        this.SetBinding(CurrentColorProperty, nameof(IMaskPageViewModel.CurrentColor));
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
                    _lines.Add(_currentLine);
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

        if (_lines.Count == 0)
        {
            return;
        }

        using var paint = new SKPaint
        {
            //Color = SKColors.Black,
            FilterQuality = SKFilterQuality.None,
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 10,
            StrokeCap = SKStrokeCap.Round,
            StrokeMiter = 0,
            StrokeJoin = SKStrokeJoin.Round,
        };

        foreach (var line in _lines)
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
        AlphaSliderContainer.IsVisible = false;
        BrushSizeSliderContainer.IsVisible = !BrushSizeSliderContainer.IsVisible;
    }

    private void Alpha_Button_Clicked(object sender, EventArgs e)
    {
        BrushSizeSliderContainer.IsVisible = false;
        AlphaSliderContainer.IsVisible = !AlphaSliderContainer.IsVisible;
    }

    private void HideSliders()
    {
        AlphaSliderContainer.IsVisible = false;
        BrushSizeSliderContainer.IsVisible = false;
    }

    private void Undo_Button_Clicked(object sender, EventArgs e)
    {
        HideSliders();

        if (!_lines.Any())
        {
            return;
        }

        _lines.Remove(_lines.Last());

        MaskCanvasView.InvalidateSurface();
    }

    private void Clear_Button_Clicked(object sender, EventArgs e)
    {
        HideSliders();

        if (!_lines.Any())
        {
            return;
        }

        _lines.Clear();

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