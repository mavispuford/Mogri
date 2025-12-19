using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

namespace MobileDiffusion.ViewModels;

public partial class HistoryItemPopupViewModel : PopupBaseViewModel, IHistoryItemPopupViewModel
{
    private readonly IFileService _fileService;
    private readonly IImageService _imageService;
    private readonly IImageGenerationService _stableDiffusionService;

    [ObservableProperty]
    private IHistoryItemViewModel _historyItem;

    [ObservableProperty]
    private ImageSource _fullImageSource;

    public HistoryItemPopupViewModel(
        IPopupService popupService,
        IFileService fileService,
        IImageService imageService,
        IImageGenerationService stableDiffusionService) : base(popupService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
    }

    public override async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.HistoryItem, out var historyItemParam) &&
            historyItemParam is IHistoryItemViewModel historyItem)
        {
            HistoryItem = historyItem;
        }
        else
        {
            // Wrap in Task.Run() so we don't crash if an exception is thrown because we are in an async void
            try
            {
                await Task.Run(async () =>
                {
                    await ClosePopupAsync();
                });
            }
            catch
            {
                // TODO - Handle this
            }
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    public override async Task OnAppearingAsync()
    {
        if (!string.IsNullOrEmpty(HistoryItem.FileName))
        {
            using var fileStream = await _fileService.GetFileStreamFromInternalStorageAsync(HistoryItem.FileName);
            var bitmap = _imageService.GetSkBitmapFromStream(fileStream);
            bitmap = _imageService.GetResizedSKBitmap(bitmap, (int)Constants.MaximumDisplayWidthHeight, (int)Constants.MaximumDisplayWidthHeight, filterImage: true, onlyIfLarger: true);

            var imageSource = new SKBitmapImageSource
            {
                Bitmap = bitmap
            };

            FullImageSource = imageSource;
        }

        if (HistoryItem.Settings == null)
        {
            _ = Task.Run(async () =>
            {
                using var imageFileStream = await _fileService.GetFileStreamFromInternalStorageAsync(HistoryItem.FileName);
                using var memoryStream = new MemoryStream();
                await imageFileStream.CopyToAsync(memoryStream);
                var imageString = Convert.ToBase64String(memoryStream.ToArray());
                var formattedImageString = string.Format(Constants.ImageDataFormat, "image/png", imageString);

                var imageInfoSettings = await _stableDiffusionService.GetImageInfoAsync(formattedImageString);

                HistoryItem.Settings = imageInfoSettings;
            });
        }
    }

    [RelayCommand]
    private async Task Delete()
    {
        var result = await Shell.Current.DisplayAlertAsync("Confirm", "Are you sure you would like to delete this image?", "DELETE", "Cancel");

        if (!result)
        {
            return;
        }

        await _fileService.DeleteFileFromInternalStorage(HistoryItem.FileName);
        await _fileService.DeleteFileFromInternalStorage(HistoryItem.ThumbnailFileName);
        
        var parameters = new Dictionary<string, object>
        {
            { NavigationParams.DeletedHistoryItem, true }
        };

        await ClosePopupAsync(parameters);
    }

    [RelayCommand]
    private async Task Close()
    {
        await ClosePopupAsync();
    }

    [RelayCommand]
    private async Task Save()
    {
        var stream = await _fileService.GetFileStreamFromInternalStorageAsync(HistoryItem.FileName);
        await _fileService.WriteImageFileToExternalStorageAsync(Path.GetFileName(HistoryItem.FileName), stream);

        await Toast.Make("Image saved.").Show();
    }

    [RelayCommand]
    private async Task UseSettings()
    {
        var parameters = new Dictionary<string, object>
        {
            { NavigationParams.PromptSettings, HistoryItem.Settings }
        };

        await ClosePopupAsync(parameters);
    }

    [RelayCommand]
    private async Task ImageInfo()
    {
        if (HistoryItem.Settings == null)
        {
            await Shell.Current.DisplayAlertAsync("No Image Info", "Unable to retrieve image info. Please try again later.", "Close");

            return;
        }

        var message = $"Prompt: {HistoryItem.Settings.Prompt}\n\n" +
            $"Negative Prompt: {HistoryItem.Settings.NegativePrompt}\n\n" +
            $"Steps: {HistoryItem.Settings.Steps}, Sampler: {HistoryItem.Settings.Sampler}\n" +
            $"Guidance Scale (Cfg): {HistoryItem.Settings.GuidanceScale}\n" + 
            $"Seed: {HistoryItem.Settings.Seed}\n" + 
            $"Size: {HistoryItem.Settings.Width}x{HistoryItem.Settings.Height}\n" +
            $"Denoising Strength: {HistoryItem.Settings.DenoisingStrength}\n" +
            $"Model: {HistoryItem.Settings.Model.DisplayName}";

        if (HistoryItem.Settings.EnableUpscaling &&
            !string.IsNullOrEmpty(HistoryItem.Settings.Upscaler) &&
            HistoryItem.Settings.UpscaleLevel > 0 &&
            HistoryItem.Settings.UpscaleSteps > 0)
        {
            message += $"\nUpscaler: {HistoryItem.Settings.Upscaler}\n" +
                $"Upscale Level: {HistoryItem.Settings.UpscaleLevel}\n" +
                $"Upscale Steps: {HistoryItem.Settings.UpscaleSteps}\n";
        }

        var result = await Shell.Current.DisplayAlertAsync("Image Info", message, "Copy to clipboard", "Close");

        if (result)
        {
            await Clipboard.Default.SetTextAsync(message);
        }
    }

    [RelayCommand]
    private async Task ImageToImage()
    {
        await SendImageBack(NavigationParams.InitImgString, true);
    }

    [RelayCommand]
    private async Task SendToCanvas()
    {
        await SendImageBack(NavigationParams.CanvasImageString, false);
    }

    private async Task SendImageBack(string parameterName, bool asFormattedString)
    {
        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var stream = await _fileService.GetFileStreamFromInternalStorageAsync(HistoryItem.FileName);

                stream.CopyTo(memoryStream);
                var imageBytes = memoryStream.ToArray();
                var imageString = Convert.ToBase64String(imageBytes);

                var parameters = new Dictionary<string, object>
                {
                    { parameterName, asFormattedString ? string.Format(Constants.ImageDataFormat, "image/png", imageString) : imageString }
                };

                await ClosePopupAsync(parameters);
            }
        }
        catch
        {
            // TODO - Handle exceptions
        }
    }
}
