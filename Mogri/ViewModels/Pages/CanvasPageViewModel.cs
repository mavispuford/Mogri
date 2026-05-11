using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Mogri.Enums;
using Mogri.Helpers;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using Mogri.Messages;
using Mogri.Models;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace Mogri.ViewModels;

/// <summary>
/// Root canvas page view model partial that owns dependency injection, observable state,
/// tool initialization, and shared state consumed across the canvas feature.
/// Sibling partials own the remaining coordination concerns:
/// - CanvasPageViewModel.Workflows.cs: save, send, crop, flatten, and patch orchestration.
/// - CanvasPageViewModel.Navigation.cs: query handling and applying returned canvas results.
/// - CanvasPageViewModel.TextAndHistory.cs: text commands, undo flow, and history popup coordination.
/// - CanvasPageViewModel.State.cs: canvas ordering, text clone helpers, and shared clear-all coordination.
/// - CanvasPageViewModel.Snapshots.cs: snapshot save/restore markers, text snapshot plumbing, and collection restore helpers.
/// - CanvasPageViewModel.Persistence.cs: auto-save and cleanup of stored canvas overlay state.
/// - CanvasPageViewModel.SourceImage.cs: source-image changes, palette updates, media replacement, bitmap decoding, and persisted restore.
/// - CanvasPageViewModel.Segmentation.cs: segmentation model readiness, interactive mask commands, and segmentation state resets.
/// Canvas-action-driven workflow layers and patch masks are built by ICanvasActionBitmapService instead of a viewmodel partial.
/// </summary>
public partial class CanvasPageViewModel : PageViewModel, ICanvasPageViewModel
{
    // Root partial constants, injected services, and shared state consumed across sibling partials.
    private const double DefaultMaskToolAlpha = 0.5d;
    private const double DefaultTextToolAlpha = 1.0d;
    private const double DefaultMaskToolNoise = 0.5d;
    private const double DefaultTextToolNoise = 0.0d;

    private enum CanvasResultTextHandling
    {
        Cancel,
        KeepEditable,
        ResolveText
    }

    private readonly object _setSegmentationImageLock = new();
    private readonly SemaphoreSlim _autoMaskSaveLock = new(1, 1);
    private readonly ICanvasActionBitmapService _canvasActionBitmapService;
    private readonly IFileService _fileService;
    private readonly IImageService _imageService;
    private readonly ICanvasBitmapService _canvasBitmapService;
    private readonly IPopupService _popupService;
    private readonly ISegmentationService _segmentationService;
    private readonly IPatchService _patchService;

    private int _imgRectIndex = 0;
    private List<int> _supportedImgRectSizes = new()
    {
        256, 512, 768, 1024, 1280, 2048
    };

    private List<Color> _colorPalette = new();
    private Color _paletteIconDarkColor = Colors.Black;
    private string? _sourceFileName;
    private bool _doingSegmentation = false;
    private CancellationTokenSource? _setSegmentationImageCancellationTokenSource;
    private int _setSegmentationImageRequestCount = 0;
    private int _setSegmentationImageVersion = 0;
    private double _maskToolAlpha = DefaultMaskToolAlpha;
    private double _textToolAlpha = DefaultTextToolAlpha;
    private double _maskToolNoise = DefaultMaskToolNoise;
    private double _textToolNoise = DefaultTextToolNoise;

    private CanvasUseMode _currentCanvasUseMode = CanvasUseMode.Inpaint;

    [ObservableProperty]
    public partial IRelayCommand? ResetZoomCommand { get; set; }

    [ObservableProperty]
    public partial List<IPaintingToolViewModel> AvailableTools { get; set; } = new();

    [ObservableProperty]
    public partial IPaintingToolViewModel CurrentTool { get; set; }

    [ObservableProperty]
    public partial double CurrentAlpha { get; set; } = DefaultMaskToolAlpha;

    [ObservableProperty]
    public partial double CurrentBrushSize { get; set; } = 10d;

    [ObservableProperty]
    public partial double CurrentNoise { get; set; } = DefaultMaskToolNoise;

    [ObservableProperty]
    public partial Color CurrentColor { get; set; } = Colors.Black;

    [ObservableProperty]
    public partial Color PaletteIconColor { get; set; } = Colors.White;

    [ObservableProperty]
    public partial double BoundingBoxScale { get; set; }

    [ObservableProperty]
    public partial float BoundingBoxSize { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CanvasActionViewModel> CanvasActions { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<TextElementViewModel> TextElements { get; set; } = new();

    [ObservableProperty]
    public partial SKBitmap? SourceBitmap { get; set; }

    [ObservableProperty]
    public partial bool SegmentationAdd { get; set; } = true;

    [ObservableProperty]
    public partial SKBitmap? SegmentationBitmap { get; set; }

    [ObservableProperty]
    public partial SKRect BoundingBox { get; set; }

    [ObservableProperty]
    public partial bool ShowMaskLayer { get; set; } = true;

    [ObservableProperty]
    public partial bool SettingSegmentationImage { get; set; } = false;

    [ObservableProperty]
    public partial bool HasSegmentationImage { get; set; } = false;

    [ObservableProperty]
    public partial bool ShowActions { get; set; }

    [ObservableProperty]
    public partial bool ShowContextMenu { get; set; } = false;

    [ObservableProperty]
    public partial bool GettingColorPalette { get; set; } = false;

    public bool IsZoomMode => CurrentTool?.Type == ToolType.Zoom;

    [ObservableProperty]
    public partial bool PreserveZoomOnNextBitmapChange { get; set; }

    [ObservableProperty]
    private IAsyncRelayCommand? _prepareForSavingCommand;

    private readonly ICanvasHistoryService _canvasHistoryService;

    // Root partial constructor sets up shared tool state; workflow and navigation logic live in sibling partials.
    public CanvasPageViewModel(
        ICanvasActionBitmapService canvasActionBitmapService,
        IFileService fileService,
        IPopupService popupService,
        IImageService imageService,
        ICanvasBitmapService canvasBitmapService,
        ISegmentationService segmentationService,
        IPatchService patchService,
        ICanvasHistoryService canvasHistoryService,
        ILoadingService loadingService) : base(loadingService)
    {
        _canvasActionBitmapService = canvasActionBitmapService ?? throw new ArgumentNullException(nameof(canvasActionBitmapService));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _canvasBitmapService = canvasBitmapService ?? throw new ArgumentNullException(nameof(canvasBitmapService));
        _segmentationService = segmentationService ?? throw new ArgumentNullException(nameof(segmentationService));
        _patchService = patchService ?? throw new ArgumentNullException(nameof(patchService));
        _canvasHistoryService = canvasHistoryService ?? throw new ArgumentNullException(nameof(canvasHistoryService));

        // Precompute the noise bitmap on a background thread so the first stroke is smooth
        NoiseShaderHelper.Initialize();

        if (Application.Current?.Resources.TryGetValue("Cadet", out var independenceColor) == true &&
            independenceColor is Color paletteIconDarkColor)
        {
            _paletteIconDarkColor = paletteIconDarkColor;
        }

        BoundingBoxSize = _supportedImgRectSizes[_imgRectIndex];

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Brush",
            IconCode = "\ue3ae",
            Effect = MaskEffect.Paint,
            Type = ToolType.PaintBrush,
            ContextButtons =
            [
                ContextButtonType.BrushSize,
                ContextButtonType.Alpha,
                ContextButtonType.ColorPicker,
                ContextButtonType.Noise
            ]
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Eraser",
            IconCode = "\ue6d0",
            Effect = MaskEffect.Erase,
            Type = ToolType.Eraser,
            ContextButtons =
            [
                ContextButtonType.BrushSize
            ]
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Magic Wand",
            IconImagePath = "wand_stars.png",
            Effect = MaskEffect.Paint,
            Type = ToolType.MagicWand,
            ContextButtons =
            [
                ContextButtonType.Alpha,
                ContextButtonType.ColorPicker,
                ContextButtonType.AddRemove,
                ContextButtonType.Noise
            ]
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Text",
            IconCode = "\uea1e",
            Effect = MaskEffect.None,
            Type = ToolType.Text,
            ContextButtons =
            [
                ContextButtonType.Alpha,
                ContextButtonType.ColorPicker,
                ContextButtonType.Noise
            ]
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Bounding Box",
            IconCode = "\ue3c6",
            Effect = MaskEffect.None,
            Type = ToolType.BoundingBox,
            ContextButtons =
            [
                ContextButtonType.BoundingBoxSize
            ]
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Eyedropper",
            IconCode = "\ue3b8",
            Effect = MaskEffect.None,
            Type = ToolType.Eyedropper,
            ContextButtons =
            [
                ContextButtonType.ColorPicker
            ]
        });

        AvailableTools.Add(new PaintingToolViewModel
        {
            Name = "Zoom",
            IconCode = "\ue8ff",
            Effect = MaskEffect.None,
            Type = ToolType.Zoom,
            ContextButtons =
            [
                ContextButtonType.ResetZoom
            ]
        });

        CurrentTool = AvailableTools.FirstOrDefault()!;

        SourceBitmap = new SKBitmap(768, 1024);
        using (var canvas = new SKCanvas(SourceBitmap))
        {
            canvas.Clear(SKColors.WhiteSmoke);
        }

        WeakReferenceMessenger.Default.Register<AppStoppedMessage>(this, (r, m) =>
        {
            _ = ((CanvasPageViewModel)r).autoSaveOrDeleteMaskAsync();
        });
    }

    partial void OnCurrentColorChanged(Color value)
    {
        if (value != null)
        {
            PaletteIconColor = value.GetLuminosity() > .8 ? _paletteIconDarkColor : Colors.White;
        }
    }

    [RelayCommand]
    private async Task ShowColorPicker()
    {
        try
        {
            if (HapticFeedback.Default.IsSupported)
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }

            var parameters = new Dictionary<string, object> {
                { NavigationParams.Color, CurrentColor },
                { NavigationParams.ColorPalette, _colorPalette },
            };

            var color = await _popupService.ShowPopupForResultAsync("ColorPickerPopup", parameters) as Color;

            if (color != null)
            {
                CurrentColor = color;
            }
        }
        catch (Exception)
        {
            await Toast.Make("Unable to show color picker.").Show();
        }
    }

    [RelayCommand]
    private void ChangeBoundingBoxSize()
    {
        _imgRectIndex = (_imgRectIndex + 1) % _supportedImgRectSizes.Count;
        BoundingBoxSize = _supportedImgRectSizes[_imgRectIndex];
    }

    [RelayCommand]
    private void ToggleMaskLayerVisibility()
    {
        ShowMaskLayer = !ShowMaskLayer;
    }

    [RelayCommand]
    private void SelectTool(IPaintingToolViewModel tool)
    {
        if (tool == null ||
            CurrentTool == tool)
        {
            return;
        }

        CurrentTool = tool;
    }

    partial void OnCurrentToolChanged(IPaintingToolViewModel value)
    {
        if (value == null)
        {
            return;
        }

        applyStoredAlphaForTool(value.Type);
        applyStoredNoiseForTool(value.Type);

        ShowContextMenu = value.Type == ToolType.MagicWand;
        OnPropertyChanged(nameof(IsZoomMode));
    }

    partial void OnCurrentAlphaChanged(double value)
    {
        switch (CurrentTool?.Type)
        {
            case ToolType.Text:
                _textToolAlpha = value;
                break;
            case ToolType.PaintBrush:
            case ToolType.MagicWand:
                _maskToolAlpha = value;
                break;
        }
    }

    partial void OnCurrentNoiseChanged(double value)
    {
        switch (CurrentTool?.Type)
        {
            case ToolType.Text:
                _textToolNoise = value;
                break;
            case ToolType.PaintBrush:
            case ToolType.MagicWand:
                _maskToolNoise = value;
                break;
        }
    }

    private void applyStoredAlphaForTool(ToolType toolType)
    {
        if (!tryGetStoredAlphaForTool(toolType, out var storedAlpha)
            || Math.Abs(CurrentAlpha - storedAlpha) < double.Epsilon)
        {
            return;
        }

        CurrentAlpha = storedAlpha;
    }

    private void applyStoredNoiseForTool(ToolType toolType)
    {
        if (!tryGetStoredNoiseForTool(toolType, out var storedNoise)
            || Math.Abs(CurrentNoise - storedNoise) < double.Epsilon)
        {
            return;
        }

        CurrentNoise = storedNoise;
    }

    private bool tryGetStoredAlphaForTool(ToolType toolType, out double alpha)
    {
        switch (toolType)
        {
            case ToolType.Text:
                alpha = _textToolAlpha;
                return true;
            case ToolType.PaintBrush:
            case ToolType.MagicWand:
                alpha = _maskToolAlpha;
                return true;
            default:
                alpha = 0d;
                return false;
        }
    }

    private bool tryGetStoredNoiseForTool(ToolType toolType, out double noise)
    {
        switch (toolType)
        {
            case ToolType.Text:
                noise = _textToolNoise;
                return true;
            case ToolType.PaintBrush:
            case ToolType.MagicWand:
                noise = _maskToolNoise;
                return true;
            default:
                noise = 0d;
                return false;
        }
    }

    [RelayCommand]
    private void ToggleActionsVisibility()
    {
        ShowActions = !ShowActions;

        vibrate(HapticFeedbackType.Click);
    }

    public override bool OnBackButtonPressed()
    {
        if (CurrentTool is { Type: ToolType.Zoom })
        {
            ResetZoomCommand?.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private void vibrate(HapticFeedbackType type)
    {
        if (HapticFeedback.Default.IsSupported)
        {
            HapticFeedback.Default.Perform(type);
        }
    }
}