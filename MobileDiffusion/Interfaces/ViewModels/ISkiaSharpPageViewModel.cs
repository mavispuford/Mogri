using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MobileDiffusion.Interfaces.ViewModels
{
    public interface ISkiaSharpPageViewModel : IPageViewModel
    {
        bool IsLoadingImage { get; set; }
        SKBitmap SourceBitmap { get; set; }
        ImageSource SavedImageSource { get; set; }

        IAsyncRelayCommand ShowMediaPickerCommand { get; }

        IAsyncRelayCommand<SKCanvasView> SaveCommand { get; }
    }
}
