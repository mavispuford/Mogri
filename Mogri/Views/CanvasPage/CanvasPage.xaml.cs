using CommunityToolkit.Mvvm.Input;
using Mogri.Enums;
using Mogri.Helpers;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using Mogri.ViewModels;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using SkiaSharp.Views.Maui;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using Mogri.Models;

namespace Mogri.Views;

/// <summary>
/// Root canvas page partial that keeps the bindable-property surface, binding handlers,
/// compiled bindings, page lifecycle wiring, and layout wiring together.
/// Sibling partials own the remaining page concerns:
/// - CanvasPage.Touch.cs: drawing, eyedropper, bounding-box, and segmentation touch routing.
/// - CanvasPage.TextInteraction.cs: text selection, dragging, scaling, rotation, and tap flow.
/// - CanvasPage.Rendering.cs: Skia paint callbacks, capture rendering, and temporary overlays.
/// - CanvasPage.Chrome.cs: slider chrome, action tray animation, timers, haptics, and tool-context button visibility.
/// </summary>
public partial class CanvasPage : BasePage
{
    // Root partial surface exposed to XAML bindings and the remaining sibling partials.
    public SKBitmap Bitmap
    {
        get => (SKBitmap)GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
    }

    public SKBitmap? SegmentationBitmap
    {
        get => (SKBitmap?)GetValue(SegmentationBitmapProperty);
        set => SetValue(SegmentationBitmapProperty, value);
    }

    public double CurrentAlpha
    {
        get => (double)GetValue(CurrentAlphaProperty);
        set => SetValue(CurrentAlphaProperty, value);
    }

    public Color CurrentColor
    {
        get => (Color)GetValue(CurrentColorProperty);
        set => SetValue(CurrentColorProperty, value);
    }

    public double CurrentBrushSize
    {
        get => (double)GetValue(CurrentBrushSizeProperty);
        set => SetValue(CurrentBrushSizeProperty, value);
    }

    public IPaintingToolViewModel CurrentTool
    {
        get => (IPaintingToolViewModel)GetValue(CurrentToolProperty);
        set => SetValue(CurrentToolProperty, value);
    }

    public ObservableCollection<CanvasActionViewModel> CanvasActions
    {
        get => (ObservableCollection<CanvasActionViewModel>)GetValue(CanvasActionsProperty);
        set => SetValue(CanvasActionsProperty, value);
    }

    public ObservableCollection<TextElementViewModel> TextElements
    {
        get => (ObservableCollection<TextElementViewModel>)GetValue(TextElementsProperty);
        set => SetValue(TextElementsProperty, value);
    }

    public SKRect BoundingBox
    {
        get => (SKRect)GetValue(BoundingBoxProperty);
        set => SetValue(BoundingBoxProperty, value);
    }

    public bool ShowBoundingBox
    {
        get => (bool)GetValue(ShowBoundingBoxProperty);
        set => SetValue(ShowBoundingBoxProperty, value);
    }

    public bool ShowMaskLayer
    {
        get => (bool)GetValue(ShowMaskLayerProperty);
        set => SetValue(ShowMaskLayerProperty, value);
    }

    public IAsyncRelayCommand<IAsyncRelayCommand> PrepareForSavingCommand
    {
        get => (IAsyncRelayCommand<IAsyncRelayCommand>)GetValue(PrepareForSavingCommandProperty);
        set => SetValue(PrepareForSavingCommandProperty, value);
    }


    public IAsyncRelayCommand<SKPoint[]> DoSegmentationCommand
    {
        get => (IAsyncRelayCommand<SKPoint[]>)GetValue(DoSegmentationCommandProperty);
        set => SetValue(DoSegmentationCommandProperty, value);
    }

    public IRelayCommand ResetZoomCommand
    {
        get => (IRelayCommand)GetValue(ResetZoomCommandProperty);
        set => SetValue(ResetZoomCommandProperty, value);
    }

    public IRelayCommand FlipSelectedTextHorizontallyCommand
    {
        get => (IRelayCommand)GetValue(FlipSelectedTextHorizontallyCommandProperty);
        set => SetValue(FlipSelectedTextHorizontallyCommandProperty, value);
    }

    public IRelayCommand FlipSelectedTextVerticallyCommand
    {
        get => (IRelayCommand)GetValue(FlipSelectedTextVerticallyCommandProperty);
        set => SetValue(FlipSelectedTextVerticallyCommandProperty, value);
    }

    public float BoundingBoxSize
    {
        get => (float)GetValue(BoundingBoxSizeProperty);
        set => SetValue(BoundingBoxSizeProperty, value);
    }

    public double BoundingBoxScale
    {
        get => (double)GetValue(BoundingBoxScaleProperty);
        set => SetValue(BoundingBoxScaleProperty, value);
    }

    public static BindableProperty BitmapProperty = BindableProperty.Create(nameof(Bitmap), typeof(SKBitmap), typeof(CanvasPage), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnSourceBitmapChanged();
    });

    public static BindableProperty SegmentationBitmapProperty = BindableProperty.Create(nameof(SegmentationBitmap), typeof(SKBitmap), typeof(CanvasPage), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnSegmentationBitmapChanged();
    });

    public static BindableProperty CurrentBrushSizeProperty = BindableProperty.Create(nameof(CurrentBrushSize), typeof(double), typeof(CanvasPage), 10d, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).AutoHideBrushSizeSlider();
    });

    public static BindableProperty CurrentAlphaProperty = BindableProperty.Create(nameof(CurrentAlpha), typeof(double), typeof(CanvasPage), .5d, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).AutoHideAlphaSlider();
    });

    public double CurrentNoise
    {
        get => (double)GetValue(CurrentNoiseProperty);
        set => SetValue(CurrentNoiseProperty, value);
    }

    public static BindableProperty CurrentNoiseProperty = BindableProperty.Create(nameof(CurrentNoise), typeof(double), typeof(CanvasPage), 0d, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).AutoHideNoiseSlider();
    });

    public static BindableProperty CurrentColorProperty = BindableProperty.Create(nameof(CurrentColor), typeof(Color), typeof(CanvasPage), Colors.Black);

    public static BindableProperty CurrentToolProperty = BindableProperty.Create(nameof(CurrentTool), typeof(IPaintingToolViewModel), typeof(CanvasPage), null, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnCurrentToolChanged();
    });

    public static BindableProperty BoundingBoxProperty = BindableProperty.Create(nameof(BoundingBox), typeof(SKRect), typeof(CanvasPage), default(SKRect));

    public static BindableProperty BoundingBoxScaleProperty = BindableProperty.Create(nameof(BoundingBoxScale), typeof(double), typeof(CanvasPage), 1d, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).UpdateBoundingBox(true, true);
    });

    public static BindableProperty BoundingBoxSizeProperty = BindableProperty.Create(nameof(BoundingBoxSize), typeof(float), typeof(CanvasPage), 0f, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).UpdateBoundingBox(true);
    });

    public static BindableProperty CanvasActionsProperty = BindableProperty.Create(nameof(CanvasActions), typeof(ObservableCollection<CanvasActionViewModel>), typeof(CanvasPage), default(ObservableCollection<CanvasActionViewModel>), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnCanvasActionsChanged(oldValue as ObservableCollection<CanvasActionViewModel>, newValue as ObservableCollection<CanvasActionViewModel>);
    });

    public static BindableProperty TextElementsProperty = BindableProperty.Create(nameof(TextElements), typeof(ObservableCollection<TextElementViewModel>), typeof(CanvasPage), default(ObservableCollection<TextElementViewModel>), propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).OnTextElementsChanged(oldValue as ObservableCollection<TextElementViewModel>, newValue as ObservableCollection<TextElementViewModel>);
    });

    public static BindableProperty PrepareForSavingCommandProperty = BindableProperty.Create(nameof(PrepareForSavingCommand), typeof(IAsyncRelayCommand), typeof(CanvasPage), default(IAsyncRelayCommand));


    public static BindableProperty DoSegmentationCommandProperty = BindableProperty.Create(nameof(DoSegmentationCommand), typeof(IAsyncRelayCommand<SKPoint[]>), typeof(CanvasPage), default(IAsyncRelayCommand<SKPoint[]>));

    public static BindableProperty ResetZoomCommandProperty = BindableProperty.Create(nameof(ResetZoomCommand), typeof(IRelayCommand), typeof(CanvasPage), default(IRelayCommand));

    public static BindableProperty FlipSelectedTextHorizontallyCommandProperty = BindableProperty.Create(nameof(FlipSelectedTextHorizontallyCommand), typeof(IRelayCommand), typeof(CanvasPage), default(IRelayCommand));

    public static BindableProperty FlipSelectedTextVerticallyCommandProperty = BindableProperty.Create(nameof(FlipSelectedTextVerticallyCommand), typeof(IRelayCommand), typeof(CanvasPage), default(IRelayCommand));

    public static BindableProperty ShowBoundingBoxProperty = BindableProperty.Create(nameof(ShowBoundingBox), typeof(bool), typeof(CanvasPage), false, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).UpdateBoundingBox(false);
    });

    public static BindableProperty ShowMaskLayerProperty = BindableProperty.Create(nameof(ShowMaskLayer), typeof(bool), typeof(CanvasPage), true, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).UpdateMaskLayer();
    });

    public static BindableProperty ShowActionsProperty = BindableProperty.Create(nameof(ShowActions), typeof(bool), typeof(CanvasPage), false, propertyChanged: (bindable, oldValue, newValue) =>
    {
        ((CanvasPage)bindable).AnimateActionsContainer((bool)newValue);
    });

    public bool ShowActions
    {
        get => (bool)GetValue(ShowActionsProperty);
        set => SetValue(ShowActionsProperty, value);
    }

    // Page-level layout state used by lifecycle and size-changed handlers.
    private bool _hasCreatedBoundingBox;

    private void OnCanvasActionsChanged(ObservableCollection<CanvasActionViewModel>? oldValue, ObservableCollection<CanvasActionViewModel>? newValue)
    {
        if (oldValue != null)
        {
            oldValue.CollectionChanged -= CanvasActions_CollectionChanged;
            foreach (var item in oldValue)
            {
                item.PropertyChanged -= OnCanvasActionPropertyChanged;
            }
        }

        if (newValue != null)
        {
            newValue.CollectionChanged += CanvasActions_CollectionChanged;
            foreach (var item in newValue)
            {
                item.PropertyChanged += OnCanvasActionPropertyChanged;
            }
        }

        MaskCanvasView.InvalidateSurface();
    }

    private void CanvasActions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (CanvasActionViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnCanvasActionPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (CanvasActionViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnCanvasActionPropertyChanged;
            }
        }

        MaskCanvasView.InvalidateSurface();
    }

    private void OnCanvasActionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        MaskCanvasView.InvalidateSurface();
    }

    private void OnTextElementsChanged(ObservableCollection<TextElementViewModel>? oldValue, ObservableCollection<TextElementViewModel>? newValue)
    {
        if (oldValue != null)
        {
            oldValue.CollectionChanged -= TextElements_CollectionChanged;
            foreach (var item in oldValue)
            {
                item.PropertyChanged -= OnTextElementPropertyChanged;
            }
        }

        if (newValue != null)
        {
            newValue.CollectionChanged += TextElements_CollectionChanged;
            foreach (var item in newValue)
            {
                item.PropertyChanged += OnTextElementPropertyChanged;
            }
        }

        TextCanvasView.InvalidateSurface();
        TemporaryCanvasView.InvalidateSurface();
    }

    private void TextElements_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (TextElementViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnTextElementPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (TextElementViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnTextElementPropertyChanged;
            }
        }

        if (_textInteraction.SelectedTextElement != null && (TextElements == null || !TextElements.Contains(_textInteraction.SelectedTextElement)))
        {
            resetTextInteractionState(clearSelection: true, clearTapState: true);
        }

        TextCanvasView.InvalidateSurface();
        TemporaryCanvasView.InvalidateSurface();
    }

    private void OnTextElementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (isTextElementRenderProperty(e.PropertyName))
        {
            TextCanvasView.InvalidateSurface();
        }

        if (sender is TextElementViewModel textElement && shouldInvalidateTemporaryCanvas(textElement, e.PropertyName))
        {
            TemporaryCanvasView.InvalidateSurface();
        }
    }

    private void OnSourceBitmapChanged()
    {
        SegmentationBitmap = null;

        UpdateCanvasSizes();

        if (BindingContext is ICanvasPageViewModel vm && vm.PreserveZoomOnNextBitmapChange)
        {
            vm.PreserveZoomOnNextBitmapChange = false;
        }
        else
        {
            ZoomContainer.Reset();
        }
    }

    private void OnSegmentationBitmapChanged()
    {
        SegmentationMaskCanvasView.InvalidateSurface();
    }

    private static bool isTextElementRenderProperty(string? propertyName)
    {
        return propertyName is nameof(TextElementViewModel.Text)
            or nameof(TextElementViewModel.X)
            or nameof(TextElementViewModel.Y)
            or nameof(TextElementViewModel.Scale)
            or nameof(TextElementViewModel.ScaleXMultiplier)
            or nameof(TextElementViewModel.ScaleYMultiplier)
            or nameof(TextElementViewModel.Rotation)
            or nameof(TextElementViewModel.Color)
            or nameof(TextElementViewModel.Alpha)
            or nameof(TextElementViewModel.Noise)
            or nameof(TextElementViewModel.IsSelected);
    }

    private static bool shouldInvalidateTemporaryCanvas(TextElementViewModel textElement, string? propertyName)
    {
        return propertyName == nameof(TextElementViewModel.IsSelected)
            || (textElement.IsSelected && isTextElementRenderProperty(propertyName));
    }

    // Root partial constructor, lifecycle hooks, and layout wiring keep page setup in one place.
    public CanvasPage()
    {
        InitializeComponent();

        this.SetBinding(BitmapProperty, nameof(ICanvasPageViewModel.SourceBitmap));
        this.SetBinding(CurrentAlphaProperty, nameof(ICanvasPageViewModel.CurrentAlpha));
        this.SetBinding(CurrentBrushSizeProperty, nameof(ICanvasPageViewModel.CurrentBrushSize));
        this.SetBinding(CurrentNoiseProperty, nameof(ICanvasPageViewModel.CurrentNoise));
        this.SetBinding(CurrentColorProperty, nameof(ICanvasPageViewModel.CurrentColor), BindingMode.TwoWay);
        this.SetBinding(CurrentToolProperty, nameof(ICanvasPageViewModel.CurrentTool));
        this.SetBinding(CanvasActionsProperty, nameof(ICanvasPageViewModel.CanvasActions), BindingMode.TwoWay);
        this.SetBinding(TextElementsProperty, nameof(ICanvasPageViewModel.TextElements), BindingMode.TwoWay);
        this.SetBinding(BoundingBoxProperty, nameof(ICanvasPageViewModel.BoundingBox), BindingMode.OneWayToSource);
        this.SetBinding(PrepareForSavingCommandProperty, nameof(ICanvasPageViewModel.PrepareForSavingCommand), BindingMode.OneWayToSource);
        this.SetBinding(BoundingBoxScaleProperty, nameof(ICanvasPageViewModel.BoundingBoxScale), BindingMode.OneWayToSource);
        this.SetBinding(BoundingBoxSizeProperty, nameof(ICanvasPageViewModel.BoundingBoxSize), BindingMode.TwoWay);
        this.SetBinding(ShowMaskLayerProperty, nameof(ICanvasPageViewModel.ShowMaskLayer), BindingMode.TwoWay);
        this.SetBinding(DoSegmentationCommandProperty, nameof(ICanvasPageViewModel.DoSegmentationCommand), BindingMode.OneWay);
        this.SetBinding(SegmentationBitmapProperty, nameof(ICanvasPageViewModel.SegmentationBitmap), BindingMode.TwoWay);
        this.SetBinding(ShowActionsProperty, nameof(ICanvasPageViewModel.ShowActions), BindingMode.OneWay);
        this.SetBinding(ResetZoomCommandProperty, nameof(ICanvasPageViewModel.ResetZoomCommand), BindingMode.OneWayToSource);
        this.SetBinding(FlipSelectedTextHorizontallyCommandProperty, nameof(ICanvasPageViewModel.FlipSelectedTextHorizontallyCommand), BindingMode.OneWayToSource);
        this.SetBinding(FlipSelectedTextVerticallyCommandProperty, nameof(ICanvasPageViewModel.FlipSelectedTextVerticallyCommand), BindingMode.OneWayToSource);

        PrepareForSavingCommand = new AsyncRelayCommand<IAsyncRelayCommand>(PrepareForSaving);
        ResetZoomCommand = new RelayCommand(() => ZoomContainer.Reset(true));
        FlipSelectedTextHorizontallyCommand = new RelayCommand(flipSelectedTextHorizontally, canFlipSelectedText);
        FlipSelectedTextVerticallyCommand = new RelayCommand(flipSelectedTextVertically, canFlipSelectedText);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        TemporaryCanvasView.SizeChanged += TemporaryCanvasView_SizeChanged;
        ActionsContainer.SizeChanged += ActionsContainer_SizeChanged;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        _ = Task.Run(async () =>
        {
            await Task.Delay(500);

            _hapticsEnabled = true;
        });
    }


    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        resetTextInteractionState(clearSelection: true, clearTapState: true);
        TemporaryCanvasView.SizeChanged -= TemporaryCanvasView_SizeChanged;
        ActionsContainer.SizeChanged -= ActionsContainer_SizeChanged;
        disposeTimers();
    }

    private void TemporaryCanvasView_SizeChanged(object? sender, EventArgs e)
    {
        if (TemporaryCanvasView.Width != -1 &&
            TemporaryCanvasView.Height != -1)
        {
            UpdateBoundingBox(true, true);
        }
    }

    private void UpdateBoundingBox(bool sizeChanged, bool resetPosition = false)
    {
        var rectSize = (float)(BoundingBoxSize / BoundingBoxScale);

        if ((!_hasCreatedBoundingBox || resetPosition) &&
            TemporaryCanvasView.Width != -1 &&
            TemporaryCanvasView.Height != -1)
        {
            BoundingBox = new SKRect(
                (float)(TemporaryCanvasView.Width / 2) - (rectSize / 2),
                (float)(TemporaryCanvasView.Height / 2) - (rectSize / 2),
                (float)(TemporaryCanvasView.Width / 2) + (rectSize / 2),
                (float)(TemporaryCanvasView.Height / 2) + (rectSize / 2));

            _hasCreatedBoundingBox = true;
        }
        else if (sizeChanged)
        {
            BoundingBox = new SKRect(
                BoundingBox.MidX - (rectSize / 2),
                BoundingBox.MidY - (rectSize / 2),
                BoundingBox.MidX + (rectSize / 2),
                BoundingBox.MidY + (rectSize / 2));
        }

        TemporaryCanvasView.InvalidateSurface();
    }

    private void UpdateCanvasSizes()
    {
        if (Bitmap == null)
        {
            return;
        }

        var scale = Math.Min((float)MaskGrid.Width / Bitmap.Width, (float)MaskGrid.Height / Bitmap.Height);
        var width = scale * Bitmap.Width;
        var height = scale * Bitmap.Height;

        SourceImageCanvasView.WidthRequest = width;
        SourceImageCanvasView.HeightRequest = height;

        TextCanvasView.WidthRequest = width;
        TextCanvasView.HeightRequest = height;

        MaskCanvasView.WidthRequest = width;
        MaskCanvasView.HeightRequest = height;

        SegmentationMaskCanvasView.WidthRequest = width;
        SegmentationMaskCanvasView.HeightRequest = height;

        TemporaryCanvasView.WidthRequest = width;
        TemporaryCanvasView.HeightRequest = height;

        // Force a measure on both canvas views because setting width/height request doesn't seem to be enough.
        SourceImageCanvasView.Measure(width, height);
        SourceImageCanvasView.InvalidateSurface();
        TextCanvasView.Measure(width, height);
        TextCanvasView.InvalidateSurface();
        MaskCanvasView.Measure(width, height);
        MaskCanvasView.InvalidateSurface();
        SegmentationMaskCanvasView.Measure(width, height);
        SegmentationMaskCanvasView.InvalidateSurface();
        TemporaryCanvasView.Measure(width, height);
        TemporaryCanvasView.InvalidateSurface();

        BoundingBoxScale = Bitmap.Width / width;
    }

    private void MaskGrid_SizeChanged(object? sender, EventArgs e)
    {
        UpdateCanvasSizes();
    }

}
