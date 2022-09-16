using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

namespace MobileDiffusion.Interfaces.ViewModels
{
    public interface ISkiaSharpPageViewModel : IPageViewModel
    {
        bool IsLoadingImage { get; set; }
        SKBitmap SourceBitmap { get; set; }

        SKCanvasView SourceCanvasView { get; set; }
        
        SKCanvasView MaskCanvasView { get; set; }

        ImageSource SavedImageSource { get; set; }

        IAsyncRelayCommand ShowMediaPickerCommand { get; }

        IAsyncRelayCommand SaveCommand { get; }
    }
}
