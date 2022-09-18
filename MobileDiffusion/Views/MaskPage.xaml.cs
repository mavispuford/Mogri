using MobileDiffusion.Interfaces.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace MobileDiffusion.Views;

public partial class MaskPage : ContentPage
{
    private List<List<SKPoint>> _paths = new();
    private List<SKPoint> _currentPath;

    public SKBitmap Bitmap
    {
        get => (SKBitmap)GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
    }

    public static BindableProperty BitmapProperty = BindableProperty.Create(nameof(Bitmap), typeof(SKBitmap), typeof(MaskPage), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((MaskPage)bindable).OnSourceBitmapChanged();
    });

    public MaskPage()
	{
		InitializeComponent();

        this.SetBinding(BitmapProperty, nameof(IMaskPageViewModel.SourceBitmap));
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