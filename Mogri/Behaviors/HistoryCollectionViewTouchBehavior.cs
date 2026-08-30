using System.Collections;
using System.Windows.Input;
using Microsoft.Maui.Controls;

#if ANDROID
using Android.Views;
using AndroidX.RecyclerView.Widget;
using AndroidX.SwipeRefreshLayout.Widget;
using Microsoft.Maui.ApplicationModel;
#endif

#if IOS || MACCATALYST
using CoreGraphics;
using Foundation;
using ObjCRuntime;
using UIKit;
#endif

namespace Mogri.Behaviors;

/// <summary>
/// Observes history item touches without taking ownership of the CollectionView touch stream.
/// </summary>
public sealed class HistoryCollectionViewTouchBehavior : Behavior<CollectionView>
{
    public static readonly BindableProperty IsEnabledProperty = BindableProperty.Create(
        nameof(IsEnabled),
        typeof(bool),
        typeof(HistoryCollectionViewTouchBehavior),
        true,
        propertyChanged: OnIsEnabledChanged);

    public static readonly BindableProperty TapCommandProperty = BindableProperty.Create(
        nameof(TapCommand),
        typeof(ICommand),
        typeof(HistoryCollectionViewTouchBehavior));

    public static readonly BindableProperty LongPressCommandProperty = BindableProperty.Create(
        nameof(LongPressCommand),
        typeof(ICommand),
        typeof(HistoryCollectionViewTouchBehavior));

    public static readonly BindableProperty LongPressDurationProperty = BindableProperty.Create(
        nameof(LongPressDuration),
        typeof(int),
        typeof(HistoryCollectionViewTouchBehavior),
        500,
        propertyChanged: OnLongPressDurationChanged);

    public static readonly BindableProperty TouchEndedCommandProperty = BindableProperty.Create(
        nameof(TouchEndedCommand),
        typeof(ICommand),
        typeof(HistoryCollectionViewTouchBehavior));

    private CollectionView? _collectionView;

#if ANDROID
    private RecyclerView? _recyclerView;
    private SwipeRefreshLayout? _swipeRefreshLayout;
    private HistoryItemTouchListener? _touchListener;
    private CancellationTokenSource? _longPressCancellation;
    private object? _pressedItem;
    private float _startX;
    private float _startY;
    private int _touchSlop;
    private bool _hasMoved;
    private bool _longPressTriggered;
#endif

#if IOS || MACCATALYST
    private UICollectionView? _nativeCollectionView;
    private UIPanGestureRecognizer? _panGestureRecognizer;
    private PanGestureTarget? _panGestureTarget;
    private UITapGestureRecognizer? _tapGestureRecognizer;
    private UILongPressGestureRecognizer? _longPressGestureRecognizer;
    private SimultaneousGestureRecognizerDelegate? _gestureDelegate;
    private bool _longPressTriggered;
#endif

    public bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    public ICommand? TapCommand
    {
        get => (ICommand?)GetValue(TapCommandProperty);
        set => SetValue(TapCommandProperty, value);
    }

    public ICommand? LongPressCommand
    {
        get => (ICommand?)GetValue(LongPressCommandProperty);
        set => SetValue(LongPressCommandProperty, value);
    }

    public int LongPressDuration
    {
        get => (int)GetValue(LongPressDurationProperty);
        set => SetValue(LongPressDurationProperty, value);
    }

    public ICommand? TouchEndedCommand
    {
        get => (ICommand?)GetValue(TouchEndedCommandProperty);
        set => SetValue(TouchEndedCommandProperty, value);
    }

    protected override void OnAttachedTo(CollectionView bindable)
    {
        base.OnAttachedTo(bindable);

        _collectionView = bindable;
        bindable.HandlerChanged += OnHandlerChanged;
        bindable.Loaded += OnLoaded;
        attachToPlatformView();
    }

    protected override void OnDetachingFrom(CollectionView bindable)
    {
        bindable.HandlerChanged -= OnHandlerChanged;
        bindable.Loaded -= OnLoaded;
        detachFromPlatformView();
        _collectionView = null;

        base.OnDetachingFrom(bindable);
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        detachFromPlatformView();
        attachToPlatformView();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        attachToPlatformView();
    }

    private static void OnIsEnabledChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is HistoryCollectionViewTouchBehavior behavior)
        {
            behavior.updatePlatformGestureState();
        }
    }

    private static void OnLongPressDurationChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is HistoryCollectionViewTouchBehavior behavior)
        {
            behavior.updateLongPressDuration();
        }
    }

    private void attachToPlatformView()
    {
#if ANDROID
        var recyclerView = findRecyclerView(_collectionView?.Handler?.PlatformView as Android.Views.View);
        if (recyclerView is not null)
        {
            var swipeRefreshLayout = findSwipeRefreshLayout(recyclerView);
            if (ReferenceEquals(_recyclerView, recyclerView))
            {
                attachToSwipeRefreshLayout(swipeRefreshLayout);
                return;
            }

            detachFromPlatformView();

            if (recyclerView.Context is not { } context)
            {
                return;
            }

            _recyclerView = recyclerView;
            _touchSlop = ViewConfiguration.Get(context)?.ScaledTouchSlop ?? 0;
            _touchListener = new HistoryItemTouchListener(this);
            recyclerView.AddOnItemTouchListener(_touchListener);
            attachToSwipeRefreshLayout(swipeRefreshLayout);
        }
#elif IOS || MACCATALYST
        var nativeCollectionView = findUICollectionView(_collectionView?.Handler?.PlatformView as UIView);
        if (nativeCollectionView is not null)
        {
            if (ReferenceEquals(_nativeCollectionView, nativeCollectionView))
            {
                return;
            }

            detachFromPlatformView();

            _nativeCollectionView = nativeCollectionView;
            _panGestureRecognizer = nativeCollectionView.PanGestureRecognizer;
            _panGestureTarget = new PanGestureTarget(this);
            // Observe the existing pan recognizer so MAUI keeps ownership of CollectionView scrolling.
            _panGestureRecognizer.AddTarget(_panGestureTarget, new Selector("handlePan:"));
            _gestureDelegate = new SimultaneousGestureRecognizerDelegate();
            _tapGestureRecognizer = new UITapGestureRecognizer(onCollectionViewTapped)
            {
                CancelsTouchesInView = false,
                Delegate = _gestureDelegate
            };
            _longPressGestureRecognizer = new UILongPressGestureRecognizer(onCollectionViewLongPressed)
            {
                CancelsTouchesInView = false,
                Delegate = _gestureDelegate
            };

            nativeCollectionView.AddGestureRecognizer(_tapGestureRecognizer);
            nativeCollectionView.AddGestureRecognizer(_longPressGestureRecognizer);
            updateLongPressDuration();
            updatePlatformGestureState();
        }
#endif
    }

    private void detachFromPlatformView()
    {
#if ANDROID
    attachToSwipeRefreshLayout(null);

        if (_recyclerView is not null)
        {
            if (_touchListener is not null)
            {
                _recyclerView.RemoveOnItemTouchListener(_touchListener);
                _touchListener = null;
            }

            cancelLongPress();
            _recyclerView = null;
        }

        _pressedItem = null;
#elif IOS || MACCATALYST
        if (_nativeCollectionView is not null)
        {
            if (_panGestureRecognizer is not null && _panGestureTarget is not null)
            {
                _panGestureRecognizer.RemoveTarget(_panGestureTarget, new Selector("handlePan:"));
            }

            if (_tapGestureRecognizer is not null)
            {
                _nativeCollectionView.RemoveGestureRecognizer(_tapGestureRecognizer);
            }

            if (_longPressGestureRecognizer is not null)
            {
                _nativeCollectionView.RemoveGestureRecognizer(_longPressGestureRecognizer);
            }
        }

        _tapGestureRecognizer?.Dispose();
        _longPressGestureRecognizer?.Dispose();
        _gestureDelegate?.Dispose();
        _tapGestureRecognizer = null;
        _longPressGestureRecognizer = null;
        _gestureDelegate = null;
        _panGestureTarget?.Dispose();
        _panGestureTarget = null;
        _panGestureRecognizer = null;
        _nativeCollectionView = null;
        _longPressTriggered = false;
#endif
    }

    private void updatePlatformGestureState()
    {
#if IOS || MACCATALYST
        if (_tapGestureRecognizer is not null)
        {
            _tapGestureRecognizer.Enabled = IsEnabled;
        }

        if (_longPressGestureRecognizer is not null)
        {
            _longPressGestureRecognizer.Enabled = IsEnabled;
        }
#endif
    }

    private void updateLongPressDuration()
    {
#if IOS || MACCATALYST
        if (_longPressGestureRecognizer is not null)
        {
            _longPressGestureRecognizer.MinimumPressDuration = Math.Max(1, LongPressDuration) / 1000d;
        }
#endif
    }

#if ANDROID
    private void onRecyclerViewInterceptTouch(MotionEvent motionEvent)
    {
        switch (motionEvent.ActionMasked)
        {
            case MotionEventActions.Down:
                handleTouchDown(motionEvent);
                break;
            case MotionEventActions.Move:
                handleTouchMove(motionEvent);
                break;
            case MotionEventActions.Up:
                handleTouchUp();
                break;
            case MotionEventActions.Cancel:
                // SwipeRefreshLayout cancels the child stream when it takes over; this is not finger release.
                resetTouchState();
                break;
        }
    }

    private void attachToSwipeRefreshLayout(SwipeRefreshLayout? swipeRefreshLayout)
    {
        if (ReferenceEquals(_swipeRefreshLayout, swipeRefreshLayout))
        {
            return;
        }

        if (_swipeRefreshLayout is not null)
        {
            _swipeRefreshLayout.Refresh -= onSwipeRefresh;
        }

        _swipeRefreshLayout = swipeRefreshLayout;

        if (_swipeRefreshLayout is not null)
        {
            _swipeRefreshLayout.Refresh += onSwipeRefresh;
        }
    }

    private void onSwipeRefresh(object? sender, EventArgs e)
    {
        // This callback is raised by the native refresh control after the actual pull-to-refresh release.
        resetTouchState();

        if (sender is SwipeRefreshLayout swipeRefreshLayout)
        {
            // Let MAUI's native handler finish publishing IsRefreshing before the async command samples state.
            swipeRefreshLayout.Post(() => executeCommand(TouchEndedCommand));
            return;
        }

        executeCommand(TouchEndedCommand);
    }


    private sealed class HistoryItemTouchListener : RecyclerView.SimpleOnItemTouchListener
    {
        private readonly HistoryCollectionViewTouchBehavior _owner;

        public HistoryItemTouchListener(HistoryCollectionViewTouchBehavior owner)
        {
            _owner = owner;
        }

        public override bool OnInterceptTouchEvent(RecyclerView recyclerView, MotionEvent motionEvent)
        {
            _owner.onRecyclerViewInterceptTouch(motionEvent);
            return false;
        }
    }
    private void handleTouchDown(MotionEvent motionEvent)
    {
        resetTouchState();

        if (!IsEnabled)
        {
            return;
        }

        _startX = motionEvent.GetX();
        _startY = motionEvent.GetY();
        _pressedItem = getItemAt(motionEvent.GetX(), motionEvent.GetY());

        if (_pressedItem is null || LongPressCommand is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _longPressCancellation = cancellation;
        _ = runLongPressAsync(_pressedItem, cancellation.Token);
    }

    private void handleTouchMove(MotionEvent motionEvent)
    {
        if (_hasMoved || _pressedItem is null)
        {
            return;
        }

        var deltaX = Math.Abs(motionEvent.GetX() - _startX);
        var deltaY = Math.Abs(motionEvent.GetY() - _startY);

        if (Math.Max(deltaX, deltaY) > _touchSlop)
        {
            _hasMoved = true;
            cancelLongPress();
        }
    }

    private void handleTouchUp()
    {
        var item = _pressedItem;
        var shouldTap = IsEnabled && !_hasMoved && !_longPressTriggered && item is not null;

        resetTouchState();

        if (shouldTap)
        {
            executeCommand(TapCommand, item);
        }

        executeCommand(TouchEndedCommand);
    }

    private async Task runLongPressAsync(object item, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Math.Max(1, LongPressDuration), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (cancellationToken.IsCancellationRequested || !IsEnabled || _hasMoved || !ReferenceEquals(_pressedItem, item))
            {
                return;
            }

            _longPressTriggered = true;
            executeCommand(LongPressCommand, item);
        });
    }

    private void resetTouchState()
    {
        cancelLongPress();
        _pressedItem = null;
        _hasMoved = false;
        _longPressTriggered = false;
    }

    private void cancelLongPress()
    {
        if (_longPressCancellation is null)
        {
            return;
        }

        _longPressCancellation.Cancel();
        _longPressCancellation.Dispose();
        _longPressCancellation = null;
    }
#endif

#if IOS || MACCATALYST
    private sealed class PanGestureTarget : NSObject
    {
        private readonly HistoryCollectionViewTouchBehavior _owner;

        public PanGestureTarget(HistoryCollectionViewTouchBehavior owner)
        {
            _owner = owner;
        }

        [Export("handlePan:")]
        public void HandlePan(UIGestureRecognizer gestureRecognizer)
        {
            _owner.onCollectionViewPanStateChanged(gestureRecognizer);
        }
    }

    private sealed class SimultaneousGestureRecognizerDelegate : UIGestureRecognizerDelegate
    {
        public override bool ShouldRecognizeSimultaneously(UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer)
        {
            return true;
        }
    }

    private static UICollectionView? findUICollectionView(UIView? view)
    {
        if (view is null)
        {
            return null;
        }

        if (view is UICollectionView collectionView)
        {
            return collectionView;
        }

        foreach (var subview in view.Subviews)
        {
            var found = findUICollectionView(subview);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void onCollectionViewTapped()
    {
        if (!IsEnabled || _tapGestureRecognizer is null || _nativeCollectionView is null)
        {
            return;
        }

        if (_longPressTriggered)
        {
            _longPressTriggered = false;
            return;
        }

        var item = getItemAt(_tapGestureRecognizer.LocationInView(_nativeCollectionView));
        executeCommand(TapCommand, item);
    }

    private void onCollectionViewLongPressed()
    {
        if (!IsEnabled || _longPressGestureRecognizer is null || _nativeCollectionView is null)
        {
            return;
        }

        if (_longPressGestureRecognizer.State == UIGestureRecognizerState.Began)
        {
            _longPressTriggered = true;
            var item = getItemAt(_longPressGestureRecognizer.LocationInView(_nativeCollectionView));
            executeCommand(LongPressCommand, item);
        }
        else if (_longPressGestureRecognizer.State is UIGestureRecognizerState.Ended or UIGestureRecognizerState.Cancelled or UIGestureRecognizerState.Failed)
        {
            _longPressTriggered = false;
        }
    }

    private void onCollectionViewPanStateChanged(UIGestureRecognizer gestureRecognizer)
    {
        // UIKit reports both completed and interrupted touches through terminal pan states.
        if (gestureRecognizer.State is UIGestureRecognizerState.Ended or UIGestureRecognizerState.Cancelled or UIGestureRecognizerState.Failed)
        {
            executeCommand(TouchEndedCommand);
        }
    }

    private object? getItemAt(CGPoint location)
    {
        if (_nativeCollectionView is null)
        {
            return null;
        }

        var indexPath = _nativeCollectionView.IndexPathForItemAtPoint(location);
        if (indexPath is null)
        {
            var hitView = _nativeCollectionView.HitTest(location, null);
            while (hitView is not null && hitView is not UICollectionViewCell && hitView != _nativeCollectionView)
            {
                hitView = hitView.Superview;
            }

            if (hitView is UICollectionViewCell cell)
            {
                indexPath = _nativeCollectionView.IndexPathForCell(cell);
            }
        }

        return indexPath is null ? null : getItemAt((int)indexPath.Item);
    }
#endif

#if ANDROID
    private static SwipeRefreshLayout? findSwipeRefreshLayout(Android.Views.View? view)
    {
        while (view is not null)
        {
            if (view is SwipeRefreshLayout swipeRefreshLayout)
            {
                return swipeRefreshLayout;
            }

            view = view.Parent as Android.Views.View;
        }

        return null;
    }

    private static RecyclerView? findRecyclerView(Android.Views.View? view)
    {
        if (view is null)
        {
            return null;
        }

        if (view is RecyclerView recyclerView)
        {
            return recyclerView;
        }

        if (view is ViewGroup viewGroup)
        {
            for (var i = 0; i < viewGroup.ChildCount; i++)
            {
                var found = findRecyclerView(viewGroup.GetChildAt(i));
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }
#endif

    private object? getItemAt(int index)
    {
        if (index < 0 || _collectionView?.ItemsSource is null)
        {
            return null;
        }

        if (_collectionView.ItemsSource is IList list)
        {
            return index < list.Count ? list[index] : null;
        }

        if (_collectionView.ItemsSource is not IEnumerable items)
        {
            return null;
        }

        var currentIndex = 0;
        foreach (var item in items)
        {
            if (currentIndex++ == index)
            {
                return item;
            }
        }

        return null;
    }

#if ANDROID
    private object? getItemAt(float x, float y)
    {
        if (_recyclerView is null)
        {
            return null;
        }

        var child = _recyclerView.FindChildViewUnder(x, y);
        if (child is null)
        {
            return null;
        }

        var position = _recyclerView.GetChildAdapterPosition(child);
        if (position == RecyclerView.NoPosition)
        {
            return null;
        }

        return getItemAt(position);
    }
#endif

    private static void executeCommand(ICommand? command, object? parameter)
    {
        if (parameter is not null && command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }

    private static void executeCommand(ICommand? command)
    {
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }
}