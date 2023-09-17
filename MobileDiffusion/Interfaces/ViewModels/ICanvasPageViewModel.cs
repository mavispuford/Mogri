using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Enums;
using MobileDiffusion.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface ICanvasPageViewModel : IPageViewModel
{
    List<IPaintingToolViewModel> AvailableTools { get; set; }

    IPaintingToolViewModel CurrentTool { get; set; }

    float CurrentAlpha { get; set; }

    Color CurrentColor { get; set; }

    SKRect BoundingBox { get; set; }

    double BoundingBoxScale { get; set; }

    float BoundingBoxSize { get; set; }

    bool IsBusy { get; set; }

    ObservableCollection<CanvasActionViewModel> CanvasActions { get; set; }

    SKCanvasView MaskCanvasView { get; set; }

    Color PaletteIconColor { get; set; }

    ImageSource SavedImageSource { get; set; }

    bool ShowMaskLayer { get; set; }

    SKBitmap SourceBitmap { get; set; }

    SKBitmap SegmentationBitmap { get; set; }

    SKCanvasView SourceCanvasView { get; set; }

    bool SettingSegmentationImage { get; set; }
    
    bool HasSegmentationImage { get; set; }

    bool ShowContextMenu { get; set; }

    bool GettingColorPalette { get; set; }

    SegmentationMode SegmentationMode { get; set; }

    IAsyncRelayCommand BeginCropImageRectCommand { get; }

    IRelayCommand ChangeBoundingBoxSizeCommand { get; }

    IAsyncRelayCommand PrepareForSavingCommand { get; set; }

    IAsyncRelayCommand FinishSavingCommand { get; }

    IAsyncRelayCommand FinishSendingToImageToImageCommand { get; }

    IAsyncRelayCommand FinishCroppingWithBoundingBoxCommand { get; }

    IAsyncRelayCommand ShowColorPickerCommand { get; }

    IAsyncRelayCommand ShowMediaPickerCommand { get; }

    IAsyncRelayCommand SaveCommand { get; }

    IAsyncRelayCommand SendToImageToImageCommand { get; }
    
    IRelayCommand ToggleMaskLayerVisibilityCommand { get; }

    IRelayCommand<IPaintingToolViewModel> SelectToolCommand { get; }

    IAsyncRelayCommand<SKPoint[]> DoSegmentationCommand { get; }

    IAsyncRelayCommand ApplySegmentationMaskCommand { get; }

    IRelayCommand ClearSegmentationMaskCommand { get; }
}
