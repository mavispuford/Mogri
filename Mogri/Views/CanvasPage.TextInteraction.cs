using Mogri.Interfaces.ViewModels.Pages;
using Mogri.Helpers;
using Mogri.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Mogri.Views;

public partial class CanvasPage
{
    // Text move-mode thresholds and selection chrome.
    private const float MinTextScale = 0.35f;
    private const float MaxTextScale = 20f;
    private const float TextSelectionPadding = 12f;
    private const float TextSelectionCornerRadius = 18f;
    private const float TextSelectionShadowStroke = 6f;
    private const float TextSelectionStroke = 3f;
    private const float DoubleTapThresholdMilliseconds = 350f;
    private const float MaxTapMovementInViewPixels = 12f;

    // Transient text selection and gesture state.
    private readonly CanvasPageTextInteractionState _textInteraction = new();

    // Text tool interaction flow.
    private void handleTextToolTouch(SKTouchEventArgs e, SKPoint viewLocation, SKPoint imageLocation)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _textInteraction.ActiveTouches[e.Id] = imageLocation;
                _textInteraction.ActiveTouchStartViewPoints[e.Id] = viewLocation;
                _textInteraction.ShouldAddTextOnTapRelease = false;
                _textInteraction.ShouldDeselectTextOnTapRelease = false;

                if (_textInteraction.ActiveTouches.Count == 1)
                {
                    var hitTextElement = CanvasTextHitTester.GetHitTextElement(TextElements, imageLocation, TextSelectionPadding, MinTextScale);
                    var hadSelection = _textInteraction.SelectedTextElement != null;

                    if (hitTextElement != null)
                    {
                        setSelectedTextElement(hitTextElement);
                        beginTextDragGesture(e.Id, imageLocation, hitTextElement);
                    }
                    else
                    {
                        _textInteraction.PrimaryTouchId = null;
                        _textInteraction.ShouldAddTextOnTapRelease = !hadSelection;
                        _textInteraction.ShouldDeselectTextOnTapRelease = hadSelection;
                    }
                }
                else
                {
                    _textInteraction.ShouldAddTextOnTapRelease = false;
                    _textInteraction.ShouldDeselectTextOnTapRelease = false;

                    if (_textInteraction.SelectedTextElement != null && _textInteraction.ActiveTouches.Count >= 2)
                    {
                        beginTextTransformGesture();
                    }
                }

                break;
            case SKTouchAction.Moved:
                if (_textInteraction.ActiveTouches.ContainsKey(e.Id))
                {
                    _textInteraction.ActiveTouches[e.Id] = imageLocation;
                }

                if (_textInteraction.SelectedTextElement == null)
                {
                    break;
                }

                if (_textInteraction.ActiveTouches.Count >= 2)
                {
                    if (!_textInteraction.IsTransformGesture)
                    {
                        beginTextTransformGesture();
                    }

                    updateTextTransformGesture();
                }
                else if (!_textInteraction.SuppressSingleTouchUntilRelease
                    && !_textInteraction.IsTransformGesture
                    && _textInteraction.PrimaryTouchId == e.Id)
                {
                    updateTextDragGesture(imageLocation);
                }

                break;
            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
            case SKTouchAction.Exited:
                completeTextToolTouch(e.Id, viewLocation, imageLocation, e.ActionType == SKTouchAction.Released);
                break;
        }
    }

    private void beginTextDragGesture(long touchId, SKPoint imageLocation, TextElementViewModel textElement)
    {
        _textInteraction.PrimaryTouchId = touchId;
        _textInteraction.DragGestureStartTouchPoint = imageLocation;
        _textInteraction.DragGestureStartElementCenter = new SKPoint(textElement.X, textElement.Y);
        _textInteraction.IsTransformGesture = false;
        _textInteraction.SuppressSingleTouchUntilRelease = false;
    }

    private void updateTextDragGesture(SKPoint imageLocation)
    {
        var selectedTextElement = _textInteraction.SelectedTextElement;
        if (selectedTextElement == null)
        {
            return;
        }

        var deltaX = imageLocation.X - _textInteraction.DragGestureStartTouchPoint.X;
        var deltaY = imageLocation.Y - _textInteraction.DragGestureStartTouchPoint.Y;

        selectedTextElement.X = _textInteraction.DragGestureStartElementCenter.X + deltaX;
        selectedTextElement.Y = _textInteraction.DragGestureStartElementCenter.Y + deltaY;
    }

    private void beginTextTransformGesture()
    {
        var selectedTextElement = _textInteraction.SelectedTextElement;
        if (selectedTextElement == null || !tryGetActiveTransformPoints(out var firstPoint, out var secondPoint))
        {
            return;
        }

        _textInteraction.TransformGestureStartDistance = Math.Max(1f, getPointDistance(firstPoint, secondPoint));
        _textInteraction.TransformGestureStartAngle = getAngleDegrees(firstPoint, secondPoint);
        _textInteraction.TransformGestureStartScale = selectedTextElement.Scale;
        _textInteraction.TransformGestureStartRotation = selectedTextElement.Rotation;
        _textInteraction.IsTransformGesture = true;
        _textInteraction.PrimaryTouchId = null;
    }

    private void updateTextTransformGesture()
    {
        var selectedTextElement = _textInteraction.SelectedTextElement;
        if (selectedTextElement == null || !tryGetActiveTransformPoints(out var firstPoint, out var secondPoint))
        {
            return;
        }

        var currentDistance = Math.Max(1f, getPointDistance(firstPoint, secondPoint));
        var scaleFactor = currentDistance / _textInteraction.TransformGestureStartDistance;
        var currentAngle = getAngleDegrees(firstPoint, secondPoint);
        var angleDelta = normalizeDegrees(currentAngle - _textInteraction.TransformGestureStartAngle);

        selectedTextElement.Scale = Math.Clamp(_textInteraction.TransformGestureStartScale * scaleFactor, MinTextScale, MaxTextScale);
        selectedTextElement.Rotation = normalizeDegrees(_textInteraction.TransformGestureStartRotation + angleDelta);
    }

    private void completeTextToolTouch(long touchId, SKPoint viewLocation, SKPoint imageLocation, bool isRelease)
    {
        var hadMultipleTouches = _textInteraction.ActiveTouches.Count > 1;
        var wasTransformGesture = _textInteraction.IsTransformGesture;
        var shouldAddTextOnTapRelease = _textInteraction.ShouldAddTextOnTapRelease;
        var shouldDeselectTextOnTapRelease = _textInteraction.ShouldDeselectTextOnTapRelease;
        var isTapCandidate = isRelease
            && !hadMultipleTouches
            && !wasTransformGesture
            && !_textInteraction.SuppressSingleTouchUntilRelease
            && isTapGesture(touchId, viewLocation);
        var tappedTextElement = isTapCandidate
            ? CanvasTextHitTester.GetHitTextElement(TextElements, imageLocation, TextSelectionPadding, MinTextScale)
            : null;

        _textInteraction.ActiveTouches.Remove(touchId);
        _textInteraction.ActiveTouchStartViewPoints.Remove(touchId);

        if (_textInteraction.ActiveTouches.Count >= 2 && _textInteraction.SelectedTextElement != null)
        {
            beginTextTransformGesture();
        }
        else if (wasTransformGesture && _textInteraction.ActiveTouches.Count == 1)
        {
            _textInteraction.IsTransformGesture = false;
            _textInteraction.PrimaryTouchId = _textInteraction.ActiveTouches.Keys.First();
            _textInteraction.SuppressSingleTouchUntilRelease = true;
        }
        else if (_textInteraction.ActiveTouches.Count == 1 && !wasTransformGesture)
        {
            _textInteraction.PrimaryTouchId = _textInteraction.ActiveTouches.Keys.First();

            if (_textInteraction.SelectedTextElement != null && _textInteraction.PrimaryTouchId.HasValue)
            {
                _textInteraction.DragGestureStartTouchPoint = _textInteraction.ActiveTouches[_textInteraction.PrimaryTouchId.Value];
                _textInteraction.DragGestureStartElementCenter = new SKPoint(_textInteraction.SelectedTextElement.X, _textInteraction.SelectedTextElement.Y);
            }
        }
        else if (_textInteraction.ActiveTouches.Count == 0)
        {
            _textInteraction.PrimaryTouchId = null;
            _textInteraction.IsTransformGesture = false;
            _textInteraction.SuppressSingleTouchUntilRelease = false;
            _textInteraction.ShouldAddTextOnTapRelease = false;
            _textInteraction.ShouldDeselectTextOnTapRelease = false;
        }

        if (isTapCandidate)
        {
            if (tappedTextElement != null)
            {
                handleTextTapGesture(tappedTextElement, viewLocation);
            }
            else if (shouldAddTextOnTapRelease)
            {
                clearLastTextTap();
                _ = PlaceTextAtPointAsync(imageLocation);
            }
            else if (shouldDeselectTextOnTapRelease)
            {
                setSelectedTextElement(null);
                clearLastTextTap();
            }
            else
            {
                clearLastTextTap();
            }
        }
        else if (_textInteraction.ActiveTouches.Count == 0)
        {
            clearLastTextTap();
        }
    }

    private void handleTextTapGesture(TextElementViewModel? tappedTextElement, SKPoint viewLocation)
    {
        if (tappedTextElement == null)
        {
            setSelectedTextElement(null);
            clearLastTextTap();
            return;
        }

        setSelectedTextElement(tappedTextElement);

        var now = DateTime.UtcNow;
        var isDoubleTap = _textInteraction.LastTapElementId == tappedTextElement.Id
            && (now - _textInteraction.LastTapTimestampUtc).TotalMilliseconds <= DoubleTapThresholdMilliseconds
            && getPointDistance(_textInteraction.LastTapViewLocation, viewLocation) <= MaxTapMovementInViewPixels;

        if (isDoubleTap)
        {
            clearLastTextTap();
            _ = editSelectedTextAsync(tappedTextElement);
            return;
        }

        _textInteraction.LastTapElementId = tappedTextElement.Id;
        _textInteraction.LastTapTimestampUtc = now;
        _textInteraction.LastTapViewLocation = viewLocation;
    }

    private async Task editSelectedTextAsync(TextElementViewModel textElement)
    {
        if (BindingContext is not ICanvasPageViewModel viewModel)
        {
            return;
        }

        resetTextInteractionState(clearSelection: false, clearTapState: true);
        await viewModel.EditTextCommand.ExecuteAsync(textElement);
    }

    private bool isTapGesture(long touchId, SKPoint viewLocation)
    {
        if (!_textInteraction.ActiveTouchStartViewPoints.TryGetValue(touchId, out var startPoint))
        {
            return false;
        }

        return getPointDistance(startPoint, viewLocation) <= MaxTapMovementInViewPixels;
    }

    private bool tryGetActiveTransformPoints(out SKPoint firstPoint, out SKPoint secondPoint)
    {
        firstPoint = default;
        secondPoint = default;

        if (_textInteraction.ActiveTouches.Count < 2)
        {
            return false;
        }

        var activePoints = _textInteraction.ActiveTouches.Values.Take(2).ToArray();
        firstPoint = activePoints[0];
        secondPoint = activePoints[1];
        return true;
    }

    private void setSelectedTextElement(TextElementViewModel? textElement)
    {
        if (ReferenceEquals(_textInteraction.SelectedTextElement, textElement))
        {
            if (_textInteraction.SelectedTextElement != null && !_textInteraction.SelectedTextElement.IsSelected)
            {
                _textInteraction.SelectedTextElement.IsSelected = true;
            }

            return;
        }

        if (_textInteraction.SelectedTextElement != null)
        {
            _textInteraction.SelectedTextElement.IsSelected = false;
        }

        _textInteraction.SelectedTextElement = textElement;

        if (_textInteraction.SelectedTextElement != null)
        {
            _textInteraction.SelectedTextElement.IsSelected = true;
        }
    }

    private void resetTextInteractionState(bool clearSelection, bool clearTapState)
    {
        _textInteraction.ResetGestureState(clearTapState);

        if (clearSelection)
        {
            setSelectedTextElement(null);
        }

        TemporaryCanvasView.InvalidateSurface();
    }

    private void clearLastTextTap()
    {
        _textInteraction.ClearTapState();
    }

    // Text move-mode geometry helpers.
    private static float getPointDistance(SKPoint firstPoint, SKPoint secondPoint)
    {
        var deltaX = secondPoint.X - firstPoint.X;
        var deltaY = secondPoint.Y - firstPoint.Y;

        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static float getAngleDegrees(SKPoint firstPoint, SKPoint secondPoint)
    {
        return MathF.Atan2(secondPoint.Y - firstPoint.Y, secondPoint.X - firstPoint.X) * (180f / MathF.PI);
    }

    private static float normalizeDegrees(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    private async Task PlaceTextAtPointAsync(SKPoint imageLocation)
    {
        if (BindingContext is ICanvasPageViewModel viewModel)
        {
            var previousTextCount = TextElements?.Count ?? 0;
            await viewModel.AddTextCommand.ExecuteAsync(imageLocation);

            if (TextElements != null && TextElements.Count > previousTextCount)
            {
                var newestTextElement = TextElements
                    .OrderByDescending(textElement => textElement.Order)
                    .FirstOrDefault();

                if (newestTextElement != null)
                {
                    clearLastTextTap();
                    setSelectedTextElement(newestTextElement);
                    TemporaryCanvasView.InvalidateSurface();
                }
            }
        }
    }
}