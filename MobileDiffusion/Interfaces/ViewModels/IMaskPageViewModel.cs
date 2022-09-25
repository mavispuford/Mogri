using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

namespace MobileDiffusion.Interfaces.ViewModels
{
    public interface IMaskPageViewModel : IPageViewModel
    {
        Color CurrentColor { get; set; }

        Color PaletteIconColor { get; set; }

        bool IsBusy { get; set; }

        List<MaskLine> Lines { get; set; }

        SKBitmap SourceBitmap { get; set; }

        SKCanvasView SourceCanvasView { get; set; }
        
        SKCanvasView MaskCanvasView { get; set; }

        ImageSource SavedImageSource { get; set; }

        SKRect InitImgRectangle { get; set; }

        bool ShowInitImgRectangle { get; set; }

        IAsyncRelayCommand PrepareForSavingCommand { get; set; }

        IAsyncRelayCommand FinishSavingCommand { get; }

        IAsyncRelayCommand ShowColorPickerCommand { get; }

        IAsyncRelayCommand ShowMediaPickerCommand { get; }

        IAsyncRelayCommand SaveMaskCommand { get; }

        IAsyncRelayCommand SaveImageCommand { get; }

        IRelayCommand ToggleInitImgRectangleCommand { get; }
    }
}
