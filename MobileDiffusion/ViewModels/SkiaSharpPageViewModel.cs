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
    private ImageSource savedImageSource;

    public SkiaSharpPageViewModel(IFileService fileService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    }

    [RelayCommand]
    private async Task Save(SKCanvasView view)
    {
        var capture = await view.CaptureAsync();

        var stream = await capture.OpenReadAsync();

        SavedImageSource = ImageSource.FromStream(() => stream);
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
            //var memoryStream = new MemoryStream();
            //await fileStream.CopyToAsync(memoryStream);

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
}
