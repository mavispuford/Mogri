using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

namespace MobileDiffusion.ViewModels;

public partial class SkiaSharpPageViewModel : PageViewModel, ISkiaSharpPageViewModel
{
    private readonly IFileService _fileService;
    
    [ObservableProperty]
    private bool isLoadingImage;

    [ObservableProperty]
    private SKBitmap sourceBitmap;

    [ObservableProperty]
    private SKCanvasView sourceCanvasView;

    [ObservableProperty]
    private SKCanvasView maskCanvasView;

    [ObservableProperty]
    private ImageSource savedImageSource;

    public SkiaSharpPageViewModel(IFileService fileService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SourceCanvasView == null ||
            MaskCanvasView == null)
        {
            return;
        }
        
        var maskCapture = await MaskCanvasView.CaptureAsync();
        var maskStream = await maskCapture.OpenReadAsync();
        var maskBitmap = SKBitmap.Decode(maskStream);

        try
        {
            var maskedResultBitmap = CreateMaskedBitmap(SourceBitmap, maskBitmap);

            var fileName = $"Mask-{DateTime.Now.Ticks}.png";

            using (var memStream = new MemoryStream())
            {
                using (var skiaStream = new SKManagedWStream(memStream))
                {
                    maskedResultBitmap.Encode(skiaStream, SKEncodedImageFormat.Png, 100);

                    memStream.Seek(0, SeekOrigin.Begin);

                    var uri = await _fileService.WriteFileToExternalStorageAsync(fileName, memStream, true);
                }
            }

            await Shell.Current.CurrentPage.DisplaySnackbar($"Image successfully saved as:\n{fileName}", duration: TimeSpan.FromSeconds(3));
        }
        catch (Exception e)
        {
            //
        }
    }

    [RelayCommand]
    private async Task ShowMediaPicker()
    {
        try
        {
            var fileResult = await MediaPicker.PickPhotoAsync();

            if (fileResult == null)
            {
                return;
            }

            IsLoadingImage = true;

            using var fileStream = await fileResult.OpenReadAsync();
            
            SourceBitmap = SKBitmap.Decode(fileStream);
        }
        catch
        {
            // TODO - Handle exceptions
        }
        finally
        {
            IsLoadingImage = false;
        }
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);
    }

    unsafe SKBitmap CreateMaskedBitmap(SKBitmap srcBitmap, SKBitmap maskBitmapFull)
    {
        if (srcBitmap == null ||
            maskBitmapFull == null)
        {
            return null;
        }

        var maskBitmap = maskBitmapFull.Resize(srcBitmap.Info, SKFilterQuality.None);

        byte* srcPtr = (byte*)srcBitmap.GetPixels().ToPointer();
        byte* mskPtr = (byte*)maskBitmap.GetPixels().ToPointer();

        int width = srcBitmap.Width;       // same for both bitmaps
        int height = srcBitmap.Height;

        SKColorType typeSrc = srcBitmap.ColorType;
        SKColorType typeMsk = maskBitmap.ColorType;

        var resultBitmap = new SKBitmap(width, height, typeSrc, SKAlphaType.Unpremul);
        
        byte* resultPtr = (byte*)resultBitmap.GetPixels().ToPointer();

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                // Get color from source bitmap
                byte srcByte1 = *srcPtr++;         // red or blue
                byte srcByte2 = *srcPtr++;         // green
                byte srcByte3 = *srcPtr++;         // blue or red
                byte srcByte4 = *srcPtr++;         // alpha

                // Get color from mask bitmap
                byte mskByte1 = *mskPtr++;         // red or blue
                byte mskByte2 = *mskPtr++;         // green
                byte mskByte3 = *mskPtr++;         // blue or red
                byte mskByte4 = *mskPtr++;         // alpha

                *resultPtr++ = srcByte1;
                *resultPtr++ = srcByte2;
                *resultPtr++ = srcByte3;

                if (mskByte4 != 0)
                { 
                    *resultPtr++ = (byte)3;
                }
                else
                {
                    *resultPtr++ = srcByte4;
                }

            }
        }

        return resultBitmap;
    }
}
