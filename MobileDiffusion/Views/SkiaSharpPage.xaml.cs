using MobileDiffusion.Controls;
using MobileDiffusion.Interfaces.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using System.Reflection;

namespace MobileDiffusion.Views;

public partial class SkiaSharpPage : ContentPage
{
    private List<List<SKPoint>> _paths = new();
    private List<SKPoint> _currentPath;

    public SKBitmap Bitmap
    {
        get => (SKBitmap)GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
    }

    public static BindableProperty BitmapProperty = BindableProperty.Create(nameof(Bitmap), typeof(SKBitmap), typeof(SkiaSharpPage), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((SkiaSharpPage)bindable).OnSourceBitmapChanged();
    });

    public SkiaSharpPage()
	{
		InitializeComponent();

        this.SetBinding(BitmapProperty, nameof(ISkiaSharpPageViewModel.SourceBitmap));
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is ISkiaSharpPageViewModel pageViewModel)
        {
            pageViewModel.SourceCanvasView = SourceImageCanvasView;
            pageViewModel.MaskCanvasView = MaskCanvasView;
        }
    }

    private void OnTouchMaskSurface(object sender, SKTouchEventArgs e)
    {
        if (e.InContact)
        {
            if (e.Location is SKPoint location)
            {
                if (_currentPath == null)
                {
                    _currentPath = new();
                    _paths.Add(_currentPath);
                }

                _currentPath.Add(location);
            }
        }
        else
        {
            _currentPath = null;
        }

        MaskCanvasView.InvalidateSurface();

        e.Handled = true;
    }

    private void OnPaintSourceImageSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        // the the canvas and properties
        var canvas = e.Surface.Canvas;

        // make sure the canvas is blank
        canvas.Clear(SKColors.Transparent);

        if (Bitmap != null)
        {
            canvas.DrawBitmap(Bitmap, Bitmap.Info.Rect, e.Info.Rect);
        }
    }

    private void OnPaintMaskSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        // the the canvas and properties
        var canvas = e.Surface.Canvas;

        // make sure the canvas is blank
        canvas.Clear(SKColors.Transparent);

        if (_paths.Count == 0)
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

        foreach (var points in _paths)
        {
            using var path = new SKPath();
            path.MoveTo(points[0]);

            for (var i = 1; i < points.Count; i++)
            {
                path.ConicTo(points[i - 1], points[i], .5f);
            }

            //path.Close();

            canvas.DrawPath(path, paint);
        }
    }

    private void Undo_Button_Clicked(object sender, EventArgs e)
    {
        if (!_paths.Any())
        {
            return;
        }

        _paths.Remove(_paths.Last());

        MaskCanvasView.InvalidateSurface();
    }

    private void Clear_Button_Clicked(object sender, EventArgs e)
    {
        if (!_paths.Any())
        {
            return;
        }

        _paths.Clear();

        MaskCanvasView.InvalidateSurface();
    }

    private async void Save_Button_Clicked(object sender, EventArgs e)
    {
        var capture = await MaskCanvasView.CaptureAsync();

        var stream = await capture.OpenReadAsync();

        ResultImageView.Source = ImageSource.FromStream(() => stream);
    }

    private void OnSourceBitmapChanged()
    {
        SourceImageCanvasView.InvalidateSurface();
    }
}