using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace MobileDiffusion.Views;

public partial class SkiaSharpPage : ContentPage
{
    private List<List<SKPoint>> _paths = new();
    private List<SKPoint> _currentPath;

    public SkiaSharpPage()
	{
		InitializeComponent();
	}

    private void OnTouch(object sender, SKTouchEventArgs e)
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

        skiaView.InvalidateSurface();

        e.Handled = true;
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
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
            IsAntialias = true,
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

        skiaView.InvalidateSurface();
    }

    private void Clear_Button_Clicked(object sender, EventArgs e)
    {
        if (!_paths.Any())
        {
            return;
        }

        _paths.Clear();

        skiaView.InvalidateSurface();
    }

    private async void Save_Button_Clicked(object sender, EventArgs e)
    {
        var capture = await skiaView.CaptureAsync();

        var stream = await capture.OpenReadAsync();

        ResultImageView.Source = ImageSource.FromStream(() => stream);
    }
}