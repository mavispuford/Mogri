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

    SKRect InitImgRectangle { get; set; }

    double InitImgRectangleScale { get; set; }

    float InitImgRectangleSize { get; set; }

    bool IsBusy { get; set; }

    ObservableCollection<CanvasActionViewModel> CanvasActions { get; set; }

    SKCanvasView MaskCanvasView { get; set; }

    Color PaletteIconColor { get; set; }

    ImageSource SavedImageSource { get; set; }

    bool ShowInitImgRectangle { get; set; }

    bool ShowMaskLayer { get; set; }

    SKBitmap SourceBitmap { get; set; }

    SKBitmap SegmentationBitmap { get; set; }

    SKCanvasView SourceCanvasView { get; set; }

    bool SettingSegmentationImage { get; set; }
    
    bool HasSegmentationImage { get; set; }

    bool ShowContextMenu { get; set; }

    SegmentationMode SegmentationMode { get; set; }

    IAsyncRelayCommand BeginCropImageRectCommand { get; }

    IRelayCommand ChangeInitImgRectangleSizeCommand { get; }

    IAsyncRelayCommand PrepareForSavingCommand { get; set; }

    IAsyncRelayCommand FinishSavingCommand { get; }

    IAsyncRelayCommand FinishSendingToImageToImageCommand { get; }

    IAsyncRelayCommand FinishCroppingInitImgRectangleCommand { get; }

    IAsyncRelayCommand ShowColorPickerCommand { get; }

    IAsyncRelayCommand ShowMediaPickerCommand { get; }

    IAsyncRelayCommand SaveCommand { get; }

    IAsyncRelayCommand SendToImageToImageCommand { get; }
    
    IRelayCommand ToggleInitImgRectangleCommand { get; }

    IRelayCommand ToggleMaskLayerVisibilityCommand { get; }

    IRelayCommand<IPaintingToolViewModel> SelectToolCommand { get; }

    IAsyncRelayCommand<SKPoint> DoSegmentationCommand { get; }

    IAsyncRelayCommand ApplySegmentationMaskCommand { get; }

    IRelayCommand ClearSegmentationMaskCommand { get; }
}
