using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

namespace MobileDiffusion.Interfaces.ViewModels
{
    public interface ICanvasPageViewModel : IPageViewModel
    {
        Color CurrentColor { get; set; }

        SKRect InitImgRectangle { get; set; }

        double InitImgRectangleScale { get; set; }

        float InitImgRectangleSize { get; set; }

        bool IsBusy { get; set; }

        List<MaskLine> Lines { get; set; }

        SKCanvasView MaskCanvasView { get; set; }

        Color PaletteIconColor { get; set; }

        ImageSource SavedImageSource { get; set; }

        bool ShowInitImgRectangle { get; set; }

        SKBitmap SourceBitmap { get; set; }

        SKCanvasView SourceCanvasView { get; set; }

        IAsyncRelayCommand BeginCropImageRectCommand { get; }

        IAsyncRelayCommand PrepareForSavingCommand { get; set; }

        IAsyncRelayCommand FinishSavingCommand { get; }

        IAsyncRelayCommand FinishSendingToImageToImageCommand { get; }


        IAsyncRelayCommand FinishCroppingInitImgRectangleCommand { get; }

        IAsyncRelayCommand ShowColorPickerCommand { get; }

        IAsyncRelayCommand ShowMediaPickerCommand { get; }

        IAsyncRelayCommand SaveCommand { get; }

        IAsyncRelayCommand SendToImageToImageCommand { get; }
        
        IRelayCommand ToggleInitImgRectangleCommand { get; }
    }
}
