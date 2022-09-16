using System.Diagnostics;

namespace MobileDiffusion.Controls;

public class GestureContainer : ContentView
{
    double _currentScale = 1;
    double _startScale = 1;
    double _scaleOffsetX = 0;
    double _scaleOffsetY = 0;

    double _scaleTargetX = 0;
    double _scaleTargetY = 0;
    double _panTargetX = 0;
    double _panTargetY = 0;

    double _initialPanX = 0;
    double _initialPanY = 0;

    DateTime _lastGestureCompleteTime;
    int _gestureTimeMinMs = 200;

    public GestureContainer()
    {
        var panGesture = new PanGestureRecognizer();
        panGesture.PanUpdated += PanGesture_PanUpdated;
        
        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += OnPinchUpdated;

        GestureRecognizers.Add(panGesture);
        GestureRecognizers.Add(pinchGesture);
    }

    private void PanGesture_PanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType == GestureStatus.Started)
        {
            _initialPanX = Content.TranslationX - _scaleTargetX;
            _initialPanY = Content.TranslationY - _scaleTargetY;

            _panTargetX = _initialPanX + e.TotalX;
            _panTargetY = _initialPanY + e.TotalY;
        }
        else if (e.StatusType == GestureStatus.Running)
        {
            _panTargetX = _initialPanX + e.TotalX;
            _panTargetY = _initialPanY + e.TotalY;
        }
        else if (e.StatusType == GestureStatus.Completed)
        {
            _lastGestureCompleteTime = DateTime.Now;
        }

        if ((DateTime.Now - _lastGestureCompleteTime).TotalMilliseconds < _gestureTimeMinMs)
        {
            return;
        }

        updateTranslation();
    }

    void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
    {

        if (e.Status == GestureStatus.Started)
        {
            // Store the current scale factor applied to the wrapped user interface element,
            // and zero the components for the center point of the translate transform.
            _startScale = Content.Scale;
            Content.AnchorX = 0;
            Content.AnchorY = 0;
        }
        else if (e.Status == GestureStatus.Running)
        {
            // Calculate the scale factor to be applied.
            _currentScale += (e.Scale - 1) * _startScale;
            _currentScale = Math.Max(1, _currentScale);

            // The ScaleOrigin is in relative coordinates to the wrapped user interface element,
            // so get the X pixel coordinate.
            double renderedX = Content.X + _scaleOffsetX;
            double deltaX = renderedX / Width;
            double deltaWidth = Width / (Content.Width * _startScale);
            double originX = (e.ScaleOrigin.X - deltaX) * deltaWidth;

            // The ScaleOrigin is in relative coordinates to the wrapped user interface element,
            // so get the Y pixel coordinate.
            double renderedY = Content.Y + _scaleOffsetY;
            double deltaY = renderedY / Height;
            double deltaHeight = Height / (Content.Height * _startScale);
            double originY = (e.ScaleOrigin.Y - deltaY) * deltaHeight;

            // Calculate the transformed element pixel coordinates.
            double targetX = _scaleOffsetX - (originX * Content.Width) * (_currentScale - _startScale);
            double targetY = _scaleOffsetY - (originY * Content.Height) * (_currentScale - _startScale);

            // Apply translation based on the change in origin.
            _scaleTargetX = Math.Clamp(targetX, -Content.Width * (_currentScale - 1), 0);
            _scaleTargetY = Math.Clamp(targetY, -Content.Height * (_currentScale - 1), 0);

            // Apply scale factor
            Content.Scale = _currentScale;
        }
        else if (e.Status == GestureStatus.Completed)
        {
            // Store the translation delta's of the wrapped user interface element.
            _scaleOffsetX = Content.TranslationX;
            _scaleOffsetY = Content.TranslationY;

            _lastGestureCompleteTime = DateTime.Now;
        }

        if ((DateTime.Now - _lastGestureCompleteTime).TotalMilliseconds < _gestureTimeMinMs)
        {
            return;
        }

        updateTranslation();
    }

    private void updateTranslation()
    {
        Content.TranslationX = _panTargetX + _scaleTargetX;
        Content.TranslationY = _panTargetY + _scaleTargetY;
    }
}
