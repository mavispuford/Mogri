using System.Windows.Input;
using MauiControls = Microsoft.Maui.Controls;

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
    private Timer? pinchTimer;

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

    public static readonly BindableProperty EnableSwipeCommandsWhenScaleIsOneProperty = BindableProperty.Create(nameof(EnableSwipeCommandsWhenScaleIsOne), typeof(bool), typeof(GestureContainer), false);

    public static readonly BindableProperty SwipeLeftCommandProperty = BindableProperty.Create(nameof(SwipeLeftCommand), typeof(ICommand), typeof(GestureContainer));

    public static readonly BindableProperty SwipeRightCommandProperty = BindableProperty.Create(nameof(SwipeRightCommand), typeof(ICommand), typeof(GestureContainer));

    public bool EnableSwipeCommandsWhenScaleIsOne
    {
        get => (bool)GetValue(EnableSwipeCommandsWhenScaleIsOneProperty);
        set => SetValue(EnableSwipeCommandsWhenScaleIsOneProperty, value);
    }

    public ICommand SwipeLeftCommand
    {
        get => (ICommand)GetValue(SwipeLeftCommandProperty);
        set => SetValue(SwipeLeftCommandProperty, value);
    }

    public ICommand SwipeRightCommand
    {
        get => (ICommand)GetValue(SwipeRightCommandProperty);
        set => SetValue(SwipeRightCommandProperty, value);
    }

    public void Reset(bool animate = false)
    {
        currentScale = 1;
        startScale = 1;
        if (Content != null)
        {
            cancelTranslationAnimations();

            if (animate)
            {
                _ = Content.ScaleToAsync(1, 250, Easing.CubicInOut);
                _ = Content.TranslateToAsync(0, 0, 250, Easing.CubicInOut);
            }
            else
            {
                Content.Scale = 1;
                Content.TranslationX = 0;
                Content.TranslationY = 0;
            }
        }
    }

    public GestureContainer()
    {
        var panGesture = new PanGestureRecognizer();
        panGesture.PanUpdated += PanGesture_PanUpdated;
        
        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += OnPinchUpdated;

        var doubleTapGesture = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTapGesture.Tapped += OnDoubleTapped;
        
        GestureRecognizers.Add(panGesture);
        GestureRecognizers.Add(pinchGesture);
        GestureRecognizers.Add(doubleTapGesture);
    }

    private async void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Content == null) return;

        if (currentScale > 1)
        {
            Reset(true);
        }
        else
        {
            var tapPosition = e.GetPosition(Content);
            if (tapPosition == null) return;

            // Zoom to 2x
            currentScale = 2;
            startScale = 2;

            double contentWidth = Content.Width;
            double contentHeight = Content.Height;

            // Center of the view
            double elementCenterX = contentWidth / 2;
            double elementCenterY = contentHeight / 2;

            // Calculate target translation to keep tap position under the finger.
            // Formula derived from: TapPos_Viewport = (TapPos_Content - Center) * Scale + Center + Translation
            // We want TapPos_Viewport_Start (Scale=1, Trans=0) == TapPos_Viewport_End (Scale=2, Trans=New)
            // TapPos_V = TapPos_C
            // TapPos_C = (TapPos_C - Center) * 2 + Center + NewTrans
            // NewTrans = TapPos_C - ((TapPos_C - Center) * 2 + Center)
            //          = TapPos_C - (2*TapPos_C - 2*Center + Center)
            //          = TapPos_C - 2*TapPos_C + Center
            //          = Center - TapPos_C
            double targetTranslationX = elementCenterX - tapPosition.Value.X;
            double targetTranslationY = elementCenterY - tapPosition.Value.Y;

            // Clamp translation so we don't show whitespace
            var maxTranslationX = Math.Max(0, (contentWidth * currentScale - contentWidth) / 2);
            var maxTranslationY = Math.Max(0, (contentHeight * currentScale - contentHeight) / 2);

            targetTranslationX = Math.Clamp(targetTranslationX, -maxTranslationX, maxTranslationX);
            targetTranslationY = Math.Clamp(targetTranslationY, -maxTranslationY, maxTranslationY);

            _ = Content.ScaleToAsync(currentScale, 250, Easing.CubicInOut);
            _ = Content.TranslateToAsync(targetTranslationX, targetTranslationY, 250, Easing.CubicInOut);
        }
    }

    void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (!EnableZooming)
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
                canZoom = true;

                break;
            case GestureStatus.Running:
                if (!canZoom)
                {
                    return;
                }


                // Calculate the scale delta
                currentScale += (e.Scale - 1) * startScale;
                currentScale = Math.Max(1, currentScale);

                Content.Scale = currentScale;

                if (currentScale == 1 && 
                    !Content.AnimationIsRunning(nameof(MauiControls.ViewExtensions.TranslateTo)) &&
                    Content.TranslationX != 0 &&
                    Content.TranslationY != 0)
                {
                    cancelTranslationAnimations();

                    canZoom = false;
                    _ = Content.TranslateToAsync(0, 0, 250u, Easing.CubicInOut);
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

    private void PanGesture_PanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!EnablePanning || !canPan)
        {
            return;
        }

        if (currentScale == 1 && !EnableSwipeCommandsWhenScaleIsOne)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                // Do nothing
                break;

            case GestureStatus.Running:
                cancelTranslationAnimations();

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

                if (currentScale > 1)
                {
                    Content.TranslationY += totalYDelta;
                }

                clampTranslation();

                prevTotalX = e.TotalX;
                prevTotalY = e.TotalY;

                break;

            case GestureStatus.Completed:
                if (currentScale == 1)
                {
                    const int swipeThreshold = 50;
                    if (prevTotalX < -swipeThreshold && SwipeLeftCommand?.CanExecute(null) == true)
                    {
                        SwipeLeftCommand.Execute(null);
                    }
                    else if (prevTotalX > swipeThreshold && SwipeRightCommand?.CanExecute(null) == true)
                    {
                        SwipeRightCommand.Execute(null);
                    }

                    _ = Content.TranslateToAsync(0, 0, 250, Easing.SpringOut);
                    isPanning = false;
                    return;
                }

                var dragX = getDrag(totalXDelta);

                Content.AnimateKinetic("TranslationAnimationX", (distance, _) =>
                {
                    if (Content.AnimationIsRunning(nameof(MauiControls.ViewExtensions.TranslateTo)))
                    {

                        return false;
                    }
                    
                    Content.TranslationX += distance;

                    clampTranslation();

                    return true;
                }, totalXDelta / 10, dragX);

                var dragY = getDrag(totalYDelta);

                Content.AnimateKinetic("TranslationAnimationY", (distance, _) =>
                {
                    if (Content.AnimationIsRunning(nameof(MauiControls.ViewExtensions.TranslateTo)))
                    {
                        return false;
                    }

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
        // Only allow panning if the zoomed content is larger than the viewport.
        // The max allowed translation is half the difference between scaled content size and viewport size.
        // This ensures the edge of the content never crosses the edge of the viewport (no whitespace).
        
        var maxTranslationX = Math.Max(0, (Content.Width * currentScale - Content.Width) / 2);
        var maxTranslationY = Math.Max(0, (Content.Height * currentScale - Content.Height) / 2);

        Content.TranslationX = Math.Clamp(Content.TranslationX, -maxTranslationX, maxTranslationX);
        Content.TranslationY = Math.Clamp(Content.TranslationY, -maxTranslationY, maxTranslationY);
    }

    private void cancelTranslationAnimations()
    {
        if (Content.AnimationIsRunning("TranslationAnimationX"))
        {
            Content.AbortAnimation("TranslationAnimationX");
        }

        if (Content.AnimationIsRunning("TranslationAnimationY"))
        {
            Content.AbortAnimation("TranslationAnimationY");
        }
    }
}
