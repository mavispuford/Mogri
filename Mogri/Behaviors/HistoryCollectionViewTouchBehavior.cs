using System.Collections;
using System.Windows.Input;
using Microsoft.Maui.Controls;

#if ANDROID
using Android.Views;
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.ApplicationModel;
#endif

#if IOS || MACCATALYST
using CoreGraphics;
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

    private CollectionView? _collectionView;

#if ANDROID
    private RecyclerView? _recyclerView;
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
    private UITapGestureRecognizer? _tapGestureRecognizer;
    private UILongPressGestureRecognizer? _longPressGestureRecognizer;
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

    protected override void OnAttachedTo(CollectionView bindable)
    {
        base.OnAttachedTo(bindable);

        _collectionView = bindable;
        bindable.HandlerChanged += OnHandlerChanged;
        attachToPlatformView();
    }

    protected override void OnDetachingFrom(CollectionView bindable)
    {
        bindable.HandlerChanged -= OnHandlerChanged;
        detachFromPlatformView();
        _collectionView = null;

        base.OnDetachingFrom(bindable);
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        detachFromPlatformView();
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
        if (_collectionView?.Handler?.PlatformView is RecyclerView recyclerView)
        {
            if (recyclerView.Context is not { } context)
            {
                return;
            }

            _recyclerView = recyclerView;
            _touchSlop = ViewConfiguration.Get(context)?.ScaledTouchSlop ?? 0;
            _touchListener = new HistoryItemTouchListener(this);
            recyclerView.AddOnItemTouchListener(_touchListener);
        }
#elif IOS || MACCATALYST
        if (_collectionView?.Handler?.PlatformView is UICollectionView collectionView)
        {
            _nativeCollectionView = collectionView;
            _tapGestureRecognizer = new UITapGestureRecognizer(onCollectionViewTapped)
            {
                CancelsTouchesInView = false
            };
            _longPressGestureRecognizer = new UILongPressGestureRecognizer(onCollectionViewLongPressed)
            {
                CancelsTouchesInView = false
            };

            collectionView.AddGestureRecognizer(_tapGestureRecognizer);
            collectionView.AddGestureRecognizer(_longPressGestureRecognizer);
            updateLongPressDuration();
            updatePlatformGestureState();
        }
#endif
    }

    private void detachFromPlatformView()
    {
#if ANDROID
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
        _tapGestureRecognizer = null;
        _longPressGestureRecognizer = null;
        _nativeCollectionView = null;
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
                resetTouchState();
                break;
        }
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
    private void onCollectionViewTapped()
    {
        if (!IsEnabled || _tapGestureRecognizer is null || _nativeCollectionView is null)
        {
            return;
        }

        var item = getItemAt(_tapGestureRecognizer.LocationInView(_nativeCollectionView));
        executeCommand(TapCommand, item);
    }

    private void onCollectionViewLongPressed()
    {
        if (!IsEnabled || _longPressGestureRecognizer is not { State: UIGestureRecognizerState.Began } || _nativeCollectionView is null)
        {
            return;
        }

        var item = getItemAt(_longPressGestureRecognizer.LocationInView(_nativeCollectionView));
        executeCommand(LongPressCommand, item);
    }

    private object? getItemAt(CGPoint location)
    {
        var indexPath = _nativeCollectionView?.IndexPathForItemAtPoint(location);
        return indexPath is null ? null : getItemAt((int)indexPath.Item);
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
}