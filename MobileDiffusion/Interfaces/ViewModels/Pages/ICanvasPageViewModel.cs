using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;
using MobileDiffusion.ViewModels;
using SkiaSharp;
using System.Collections.ObjectModel;
using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.ViewModels.Pages;

public interface ICanvasPageViewModel : IPageViewModel
{
    List<IPaintingToolViewModel> AvailableTools { get; set; }

    IPaintingToolViewModel CurrentTool { get; set; }

    double CurrentAlpha { get; set; }

    double CurrentBrushSize { get; set; }

    double CurrentNoise { get; set; }

    Color CurrentColor { get; set; }

    SKRect BoundingBox { get; set; }

    double BoundingBoxScale { get; set; }

    float BoundingBoxSize { get; set; }

    bool IsBusy { get; set; }

    ObservableCollection<CanvasActionViewModel> CanvasActions { get; set; }

    Color PaletteIconColor { get; set; }

    ImageSource SavedImageSource { get; set; }

    bool ShowMaskLayer { get; set; }

    SKBitmap SourceBitmap { get; set; }

    bool SegmentationAdd { get; }

    SKBitmap SegmentationBitmap { get; set; }

    bool SettingSegmentationImage { get; set; }
    
    bool HasSegmentationImage { get; set; }

    IRelayCommand UndoCommand { get; }

    IAsyncRelayCommand ClearCommand { get; }

    bool ShowContextMenu { get; set; }

    bool GettingColorPalette { get; set; }

    bool ShowActions {get; set; }

    IAsyncRelayCommand BeginCropImageRectCommand { get; }

    IRelayCommand ChangeBoundingBoxSizeCommand { get; }

    IAsyncRelayCommand PrepareForSavingCommand { get; set; }

    IAsyncRelayCommand<CanvasCaptureResult> FinishSavingCommand { get; }

    IAsyncRelayCommand<CanvasCaptureResult> FinishSendingToImageToImageCommand { get; }

    IAsyncRelayCommand<CanvasCaptureResult> FinishCroppingWithBoundingBoxCommand { get; }

    IAsyncRelayCommand ShowColorPickerCommand { get; }

    IAsyncRelayCommand ShowMediaPickerCommand { get; }

    IAsyncRelayCommand SaveCommand { get; }

    IAsyncRelayCommand SendToImageToImageCommand { get; }
    
    IRelayCommand ToggleMaskLayerVisibilityCommand { get; }

    IRelayCommand<IPaintingToolViewModel> SelectToolCommand { get; }

    IAsyncRelayCommand<SKPoint[]> DoSegmentationCommand { get; }

    IAsyncRelayCommand ApplySegmentationMaskCommand { get; }

    IRelayCommand ClearSegmentationMaskCommand { get; }

    IRelayCommand ToggleSegmentationAddCommand { get; }

    IRelayCommand ToggleActionsVisibilityCommand { get; }

    IRelayCommand ResetZoomCommand { get; set; }
}
