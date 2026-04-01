using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Mogri.Models;

namespace Mogri.ViewModels;

public partial class HistoryItemPopupViewModel : PopupBaseViewModel, IHistoryItemPopupViewModel
{
    private readonly IFileService _fileService;
    private readonly IImageService _imageService;
    private readonly IImageGenerationService _stableDiffusionService;

    private IList<IHistoryItemViewModel>? _historyItems;

    [ObservableProperty]
    public partial IHistoryItemViewModel? HistoryItem { get; set; }

    [ObservableProperty]
    public partial ImageSource? FullImageSource { get; set; }

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

        if (query.TryGetValue(NavigationParams.HistoryItems, out var historyItemsParam) &&
            historyItemsParam is IList<IHistoryItemViewModel> historyItems)
        {
            _historyItems = historyItems;
        }

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
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to close popup: {ex}");
            }
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    partial void OnHistoryItemChanged(IHistoryItemViewModel? value)
    {
        _ = LoadImageAsync();
    }

    private async Task LoadImageAsync()
    {
        var currentItem = HistoryItem;
        if (currentItem == null)
        {
            return;
        }

        // Delay to allow the UI to settle (e.g. keyboard hiding, popup animation)
        await Task.Delay(100);

        if (!string.IsNullOrEmpty(currentItem.FileName))
        {
            SKBitmapImageSource? imageSource = null;

            await Task.Run(async () =>
            {
                using var fileStream = await _fileService.GetFileStreamFromInternalStorageAsync(currentItem.FileName);

                if (fileStream == null)
                {
                    return;
                }

                var originalBitmap = _imageService.GetSkBitmapFromStream(fileStream);
                var resizedBitmap = _imageService.GetResizedSKBitmap(originalBitmap, (int)Constants.MaximumDisplayWidthHeight, (int)Constants.MaximumDisplayWidthHeight, filterImage: true, onlyIfLarger: true);

                imageSource = new SKBitmapImageSource
                {
                    Bitmap = resizedBitmap
                };
            });

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (HistoryItem == currentItem && imageSource != null)
                {
                    FullImageSource = imageSource;
                }
            });
        }

        if (currentItem.Settings == null)
        {
            _ = Task.Run(async () =>
            {
                using var imageFileStream = await _fileService.GetFileStreamFromInternalStorageAsync(currentItem.FileName);
                if (imageFileStream == null) return;

                using var memoryStream = new MemoryStream();
                await imageFileStream.CopyToAsync(memoryStream);
                var imageString = Convert.ToBase64String(memoryStream.ToArray());
                var formattedImageString = string.Format(Constants.ImageDataFormat, "image/png", imageString);

                var imageInfoSettings = await _stableDiffusionService.GetImageInfoAsync(formattedImageString);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    currentItem.Settings = imageInfoSettings;
                    
                    // On iOS, we have to manually call this for the binding to pick up the change
                    OnPropertyChanged(nameof(HistoryItem));
                });
            });
        }
    }

    [RelayCommand]
    private void NextItem()
    {
        if (_historyItems == null || HistoryItem == null)
        {
            return;
        }

        var index = _historyItems.IndexOf(HistoryItem);
        if (index < _historyItems.Count - 1)
        {
            HistoryItem = _historyItems[index + 1];
        }
    }

    [RelayCommand]
    private void PreviousItem()
    {
        if (_historyItems == null || HistoryItem == null)
        {
            return;
        }

        var index = _historyItems.IndexOf(HistoryItem);
        if (index > 0)
        {
            HistoryItem = _historyItems[index - 1];
        }
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (HistoryItem == null) return;

        var result = await _popupService.DisplayAlertAsync("Confirm", "Are you sure you would like to delete this image?", "DELETE", "Cancel");

        if (!result)
        {
            return;
        }

        await _fileService.DeleteFileFromInternalStorageAsync(HistoryItem.FileName);
        await _fileService.DeleteFileFromInternalStorageAsync(HistoryItem.ThumbnailFileName);

        var parameters = new Dictionary<string, object>
        {
            { NavigationParams.DeletedHistoryItem, HistoryItem }
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
        if (HistoryItem == null) return;

        var stream = await _fileService.GetFileStreamFromInternalStorageAsync(HistoryItem.FileName);

        if (stream == null) return;

        await _fileService.WriteImageFileToExternalStorageAsync(Path.GetFileName(HistoryItem.FileName), stream);

        await Toast.Make("Image saved.").Show();
    }

    [RelayCommand]
    private async Task UseSettings()
    {
        if (HistoryItem == null) return;

        var parameters = new Dictionary<string, object>
        {
            { NavigationParams.PromptSettings, HistoryItem.Settings ?? new PromptSettings() }
        };

        await ClosePopupAsync(parameters);
    }

    [RelayCommand]
    private async Task ImageInfo()
    {
        if (HistoryItem?.Settings == null)
        {
            await _popupService.DisplayAlertAsync("No Image Info", "Unable to retrieve image info. Please try again later.", "Close");

            return;
        }

        var message = $"Prompt: {HistoryItem.Settings.Prompt}\n\n" +
            $"Negative Prompt: {HistoryItem.Settings.NegativePrompt}\n\n" +
            $"Steps: {HistoryItem.Settings.Steps}, Sampler: {HistoryItem.Settings.Sampler}\n" +
            $"Guidance Scale (Cfg): {HistoryItem.Settings.GuidanceScale}\n" +
            $"Seed: {HistoryItem.Settings.Seed}\n" +
            $"Size: {HistoryItem.Settings.Width}x{HistoryItem.Settings.Height}\n" +
            $"Denoising Strength: {HistoryItem.Settings.DenoisingStrength}\n" +
            $"Model: {HistoryItem.Settings.Model?.DisplayName ?? "Unknown"}";

        if (!string.IsNullOrEmpty(HistoryItem.Settings.Scheduler))
        {
            message += $"\nScheduler: {HistoryItem.Settings.Scheduler}";
        }

        if (HistoryItem.Settings.DistilledCfgScale.HasValue)
        {
            message += $"\nDistilled CFG Scale: {HistoryItem.Settings.DistilledCfgScale}";
        }

        if (HistoryItem.Settings.EnableUpscaling &&
            !string.IsNullOrEmpty(HistoryItem.Settings.Upscaler) &&
            HistoryItem.Settings.UpscaleLevel > 0 &&
            HistoryItem.Settings.UpscaleSteps > 0)
        {
            message += $"\nUpscaler: {HistoryItem.Settings.Upscaler}\n" +
                $"Upscale Level: {HistoryItem.Settings.UpscaleLevel}\n" +
                $"Upscale Steps: {HistoryItem.Settings.UpscaleSteps}\n";
        }

        var result = await _popupService.DisplayAlertAsync("Image Info", message, "Copy to clipboard", "Close");

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
        if (HistoryItem == null) return;

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var stream = await _fileService.GetFileStreamFromInternalStorageAsync(HistoryItem.FileName);
                if (stream == null) return;

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
        catch (Exception)
        {
            await _popupService.DisplayAlertAsync("Error", "Failed to process image", "OK");
        }
    }
}
