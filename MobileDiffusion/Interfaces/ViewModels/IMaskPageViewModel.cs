using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

namespace MobileDiffusion.Interfaces.ViewModels
{
    public interface IMaskPageViewModel : IPageViewModel
    {
        Color CurrentColor { get; set; }

        Color PaletteIconColor { get; set; }

        bool IsBusy { get; set; }

        SKBitmap SourceBitmap { get; set; }

        SKCanvasView SourceCanvasView { get; set; }
        
        SKCanvasView MaskCanvasView { get; set; }

        ImageSource SavedImageSource { get; set; }

        IAsyncRelayCommand ShowColorPickerCommand { get; }

        IAsyncRelayCommand ShowMediaPickerCommand { get; }

        IAsyncRelayCommand SaveCommand { get; }
    }
}
