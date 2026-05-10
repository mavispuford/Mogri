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
/// Root canvas page partial that keeps the bindable-property surface, compiled bindings,
/// and page lifecycle wiring together.
/// Sibling partials own the remaining page concerns:
/// - CanvasPage.Bindings.cs: collection subscriptions and surface invalidation.
/// - CanvasPage.Touch.cs: drawing, eyedropper, bounding-box, and segmentation touch routing.
/// - CanvasPage.TextInteraction.cs: text selection, dragging, scaling, rotation, and tap flow.
/// - CanvasPage.Rendering.cs: Skia paint callbacks, capture rendering, and temporary overlays.
/// - CanvasPage.Chrome.cs: slider chrome, action tray animation, timers, haptics, and bounding-box helpers.
/// </summary>
public partial class CanvasPage : BasePage
{
    // Root partial surface exposed to XAML bindings and sibling partials.
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

    // Root partial constructor and lifecycle hooks keep the page wiring in one place.
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

        PrepareForSavingCommand = new AsyncRelayCommand<IAsyncRelayCommand>(PrepareForSaving);
        ResetZoomCommand = new RelayCommand(() => ZoomContainer.Reset(true));
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

}
