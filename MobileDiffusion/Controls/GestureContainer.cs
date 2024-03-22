namespace MobileDiffusion.Controls;

public class GestureContainer : ContentView
{
    private const int afterPinchDelay = 100;
    private const double maxDelta = 50;
    private double currentScale = 1;
    private double startScale = 1;
    private Point startOffset = new ();
    private bool canPan = true;
    private bool canZoom = true;
    private bool isPanning;
    private double scaleOriginX;
    private double scaleOriginY;
    private double scalePivotX;
    private double scalePivotY;
    private double prevTotalX;
    private double prevTotalY;
    private double totalXDelta;
    private double totalYDelta;
    private Timer pinchTimer;

    public bool EnablePanning
    {
        get => (bool)GetValue(EnablePanningProperty);
        set => SetValue(EnablePanningProperty, value);
    }

    public bool EnableZooming
    {
        get => (bool)GetValue(EnableZoomingProperty);
        set => SetValue(EnableZoomingProperty, value);
    }

    public static readonly BindableProperty EnablePanningProperty = BindableProperty.Create(nameof(EnablePanning), typeof(bool), typeof(GestureContainer), true);

    public static readonly BindableProperty EnableZoomingProperty = BindableProperty.Create(nameof(EnableZooming), typeof(bool), typeof(GestureContainer), true);

    public GestureContainer()
    {
        var panGesture = new PanGestureRecognizer();
        panGesture.PanUpdated += PanGesture_PanUpdated;
        
        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += OnPinchUpdated;
        
        GestureRecognizers.Add(panGesture);
        GestureRecognizers.Add(pinchGesture);
    }

    void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
    {
        if (!EnableZooming || !canZoom)
        {
            return;
        }

        // Handle the pinch gestures
        switch (e.Status)
        {
            case GestureStatus.Started:
                // Store the current scale factor to make deltas relative
                startScale = currentScale;
                // Store the current offset
                startOffset = new Point(Content.TranslationX / currentScale, Content.TranslationY / currentScale);

                // Convert ScaleOrigin to actual coordinates
                scaleOriginX = Content.Width * e.ScaleOrigin.X;
                scaleOriginY = Content.Height * e.ScaleOrigin.Y;

                scalePivotX = scaleOriginX - (Content.Width / 2);
                scalePivotY = scaleOriginY - (Content.Height / 2);

                canPan = false;

                break;
            case GestureStatus.Running:
                // Calculate the scale delta
                currentScale += (e.Scale - 1) * startScale;
                currentScale = Math.Max(1, currentScale);

                Content.Scale = currentScale;

                if (currentScale == 1 && 
                    !Content.AnimationIsRunning(nameof(Microsoft.Maui.Controls.ViewExtensions.TranslateTo)) &&
                    Content.TranslationX != 0 &&
                    Content.TranslationY != 0)
                {
                    canZoom = false;
                    Content.TranslateTo(0, 0, 250u, Easing.CubicInOut).ContinueWith(async arg =>
                    {
                        await Task.Delay(100);
                        
                        canZoom = true;
                    });
                }
                else
                {
                    var scaleFactor = (1 - currentScale / startScale);
                    var newXOffset = (startOffset.X * currentScale) + (scalePivotX * scaleFactor);
                    var newYOffset = (startOffset.Y * currentScale) + (scalePivotY * scaleFactor);

                    Content.TranslationX = newXOffset;
                    Content.TranslationY = newYOffset;

                    clampTranslation();
                }

                break;

            case GestureStatus.Completed:
                // Store the final scale
                startScale = currentScale;

                try
                {
                    pinchTimer?.Dispose();
                    // Add a small timer to enable panning again to avoid weird motion after a pinch
                    pinchTimer = new(state =>
                    {
                        canPan = true;
                    }, null, afterPinchDelay, Timeout.Infinite);
                }
                catch
                {
                    canPan = true;
                }
                break;
        }
    }

    private void PanGesture_PanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (!EnablePanning || !canPan || currentScale == 1)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                // Do nothing
                break;

            case GestureStatus.Running:
                if (Content.AnimationIsRunning("TranslationAnimationX"))
                {
                    Content.AbortAnimation("TranslationAnimationX");
                }

                if (Content.AnimationIsRunning("TranslationAnimationY"))
                {
                    Content.AbortAnimation("TranslationAnimationY");
                }

                // This lives here instead of in the Started status because if the user is pinching to zoom and they lift one finger,
                // the pan gesture jumps straight to the Running status instead of hitting Started first.
                if (!isPanning)
                {
                    totalXDelta = 0;
                    totalYDelta = 0;
                    prevTotalX = 0;
                    prevTotalY = 0;
                    isPanning = true;
                }

                totalXDelta = e.TotalX - prevTotalX;
                totalYDelta = e.TotalY - prevTotalY;

                // Prevent jumps if the user was pinching to zoom and they lift one finger up while the other is still touching
                if (totalXDelta > maxDelta || totalYDelta > maxDelta)
                {
                    totalXDelta = 0;
                    totalYDelta = 0;
                }

                Content.TranslationX += totalXDelta;
                Content.TranslationY += totalYDelta;

                clampTranslation();

                prevTotalX = e.TotalX;
                prevTotalY = e.TotalY;

                break;

            case GestureStatus.Completed:
                var dragX = getDrag(totalXDelta);

                Content.AnimateKinetic("TranslationAnimationX", (distance, velocityX) =>
                {
                    Content.TranslationX += distance;

                    clampTranslation();

                    return true;
                }, totalXDelta / 10, dragX);

                var dragY = getDrag(totalYDelta);

                Content.AnimateKinetic("TranslationAnimationY", (distance, velocityY) =>
                {
                    Content.TranslationY += distance;

                    clampTranslation();

                    return true;
                }, totalYDelta / 10, dragY);

                isPanning = false;
                break;
        }
    }

    /// <summary>
    ///     Gets an adjusted drag value based on the input velocity.
    /// </summary>
    /// <param name="velocity">The velocity.</param>
    /// <returns>The drag value.</returns>
    private static double getDrag(double velocity)
    {
        return velocity switch
        {
            > maxDelta / 2 => .006,
            > maxDelta / 3 => .004,
            _ => .002
        };
    }

    private void clampTranslation()
    {
        var maxTranslationX = (Content.Width / 2) * currentScale;
        var maxTranslationY = (Content.Height / 2) * currentScale;

        Content.TranslationX = double.Clamp(Content.TranslationX, -maxTranslationX, maxTranslationX);
        Content.TranslationY = double.Clamp(Content.TranslationY, -maxTranslationY, maxTranslationY);
    }
}
