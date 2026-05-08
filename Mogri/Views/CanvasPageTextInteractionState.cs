using Mogri.ViewModels;
using SkiaSharp;

namespace Mogri.Views;

/// <summary>
/// Stores transient text-selection and gesture state for the canvas page.
/// </summary>
internal sealed class CanvasPageTextInteractionState
{
    public Dictionary<long, SKPoint> ActiveTouches { get; } = new();

    public Dictionary<long, SKPoint> ActiveTouchStartViewPoints { get; } = new();

    public TextElementViewModel? SelectedTextElement { get; set; }

    public long? PrimaryTouchId { get; set; }

    public SKPoint DragGestureStartTouchPoint { get; set; }

    public SKPoint DragGestureStartElementCenter { get; set; }

    public float TransformGestureStartDistance { get; set; }

    public float TransformGestureStartAngle { get; set; }

    public float TransformGestureStartScale { get; set; }

    public float TransformGestureStartRotation { get; set; }

    public bool IsTransformGesture { get; set; }

    public bool SuppressSingleTouchUntilRelease { get; set; }

    public DateTime LastTapTimestampUtc { get; set; } = DateTime.MinValue;

    public string? LastTapElementId { get; set; }

    public SKPoint LastTapViewLocation { get; set; }

    public void ResetGestureState(bool clearTapState)
    {
        ActiveTouches.Clear();
        ActiveTouchStartViewPoints.Clear();
        PrimaryTouchId = null;
        DragGestureStartTouchPoint = default;
        DragGestureStartElementCenter = default;
        TransformGestureStartDistance = 0f;
        TransformGestureStartAngle = 0f;
        TransformGestureStartScale = 0f;
        TransformGestureStartRotation = 0f;
        IsTransformGesture = false;
        SuppressSingleTouchUntilRelease = false;

        if (clearTapState)
        {
            ClearTapState();
        }
    }

    public void ClearTapState()
    {
        LastTapElementId = null;
        LastTapTimestampUtc = DateTime.MinValue;
        LastTapViewLocation = default;
    }
}