using Microsoft.Maui.Controls;
using MobileDiffusion.Helpers;
using System.Diagnostics;

namespace MobileDiffusion.Controls;

public class GestureContainer : ContentView
{
    private const double maxDelta = 50;
    private double currentScale = 1;
    private double startScale = 1;
    private Point startOffset = new ();
    private bool isPanning;
    private double prevTotalX;
    private double prevTotalY;
    private double totalXDelta;
    private double totalYDelta;

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
        // Ignore pinch events when no ViewPortOrigin is defined
        if (e.Status == GestureStatus.Started)
        {
            // Store the current scale factor to make deltas relative
            startScale = currentScale;
            // Store the current offset
            startOffset = new Point(Content.TranslationX / currentScale, Content.TranslationY / currentScale);
        }

        // Handle the pinch gestures
        switch (e.Status)
        {
            case GestureStatus.Running:
                // Calculate the scale delta
                currentScale += (e.Scale - 1) * startScale;
                currentScale = Math.Max(1, currentScale);

                var newXOffset = (startOffset.X + e.ScaleOrigin.X) * currentScale;
                var newYOffset = (startOffset.Y + e.ScaleOrigin.Y) * currentScale;

                // Apply the scale and offset
                Content.Scale = currentScale;
                Content.TranslationX = newXOffset;
                Content.TranslationY = newYOffset;

                clampTranslation();

                break;

            case GestureStatus.Completed:
                // Store the final scale
                startScale = currentScale;
                break;
        }
    }

    private void PanGesture_PanUpdated(object sender, PanUpdatedEventArgs e)
    {
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
