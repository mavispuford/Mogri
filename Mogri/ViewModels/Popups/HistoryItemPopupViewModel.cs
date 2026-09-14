using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Helpers;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;
using Mogri.Models;

namespace Mogri.ViewModels;

public partial class HistoryItemPopupViewModel : PopupBaseViewModel, IHistoryItemPopupViewModel
{
    private readonly IFileService _fileService;
    private readonly IHistoryService _historyService;
    private readonly IImageService _imageService;
    private readonly IImageGenerationCoordinator _stableDiffusionService;
    private readonly IToastService _toastService;
    private readonly IMainThreadService _mainThreadService;

    private IList<IHistoryItemViewModel>? _historyItems;
    private Task _imageLoadTask = Task.CompletedTask;

    [ObservableProperty]
    public partial IHistoryItemViewModel? HistoryItem { get; set; }

    [ObservableProperty]
    public partial ImageSource? FullImageSource { get; set; }

    public HistoryItemPopupViewModel(
        IPopupService popupService,
        IFileService fileService,
        IHistoryService historyService,
        IImageService imageService,
        IImageGenerationCoordinator stableDiffusionService,
        IToastService toastService,
        IMainThreadService mainThreadService) : base(popupService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
        _mainThreadService = mainThreadService ?? throw new ArgumentNullException(nameof(mainThreadService));
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
        _imageLoadTask = value == null ? Task.CompletedTask : LoadImageAsync(value);
    }

    private async Task LoadImageAsync(IHistoryItemViewModel currentItem)
    {
        // Delay to allow the UI to settle (e.g. keyboard hiding, popup animation)
        await Task.Delay(100);

        if (!string.IsNullOrEmpty(currentItem.FileName))
        {
            SKBitmapImageSource? imageSource = null;
            int? actualWidth = null;
            int? actualHeight = null;

            await Task.Run(async () =>
            {
                using var fileStream = await _fileService.GetFileStreamFromInternalStorageAsync(currentItem.FileName);

                if (fileStream == null)
                {
                    return;
                }

                var originalBitmap = _imageService.GetSkBitmapFromStream(fileStream);
                if (originalBitmap == null)
                {
                    return;
                }

                actualWidth = originalBitmap.Width;
                actualHeight = originalBitmap.Height;
                var resizedBitmap = _imageService.GetResizedSKBitmap(originalBitmap, (int)Constants.MaximumDisplayWidthHeight, (int)Constants.MaximumDisplayWidthHeight, filterImage: true, onlyIfLarger: true);
                if (resizedBitmap == null)
                {
                    originalBitmap.Dispose();
                    return;
                }

                if (!ReferenceEquals(originalBitmap, resizedBitmap))
                {
                    originalBitmap.Dispose();
                }

                imageSource = new SKBitmapImageSource
                {
                    Bitmap = resizedBitmap
                };
            });

            await _mainThreadService.InvokeOnMainThreadAsync(() =>
            {
                if (HistoryItem == currentItem)
                {
                    ApplyActualDimensions(currentItem.Settings, actualWidth, actualHeight);

                    if (imageSource != null)
                    {
                        FullImageSource = imageSource;
                    }
                }
            });

            if (currentItem.Settings == null)
            {
                _ = Task.Run(async () =>
                {
                    using var imageFileStream = await _fileService.GetFileStreamFromInternalStorageAsync(currentItem.FileName);
                    if (imageFileStream == null) return;

                    var imageInfoSettings = await _stableDiffusionService.GetImageInfoAsync(imageFileStream);
                    ApplyActualDimensions(imageInfoSettings, actualWidth, actualHeight);

                    await _mainThreadService.InvokeOnMainThreadAsync(() =>
                    {
                        currentItem.Settings = imageInfoSettings;

                        // On iOS, we have to manually call this for the binding to pick up the change
                        OnPropertyChanged(nameof(HistoryItem));
                    });
                });
            }
        }
    }

    private static void ApplyActualDimensions(PromptSettings? settings, int? actualWidth, int? actualHeight)
    {
        if (settings == null)
        {
            return;
        }

        if (actualWidth.HasValue)
        {
            settings.ActualWidth ??= actualWidth.Value;
        }

        if (actualHeight.HasValue)
        {
            settings.ActualHeight ??= actualHeight.Value;
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

        await _historyService.DeleteItemsAsync(new List<HistoryEntity> { HistoryItem.Entity });

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

        await _toastService.ShowAsync("Image saved.");
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
        var currentItem = HistoryItem;
        var settings = currentItem?.Settings;
        if (settings == null)
        {
            await _popupService.DisplayAlertAsync("No Image Info", "Unable to retrieve image info. Please try again later.", "Close");

            return;
        }

        if (!settings.ActualWidth.HasValue || !settings.ActualHeight.HasValue)
        {
            await _imageLoadTask;

            if (!ReferenceEquals(HistoryItem, currentItem))
            {
                return;
            }
        }

        var (prompt, negativePrompt) = settings.GetCombinedPromptAndPromptStyles();
        var actualWidth = settings.ActualWidth;
        var actualHeight = settings.ActualHeight;

        var size = $"{settings.Width}x{settings.Height}";
        if (actualWidth.HasValue && actualHeight.HasValue &&
            (actualWidth.Value != settings.Width || actualHeight.Value != settings.Height))
        {
            size += $" (Actual: {actualWidth.Value}x{actualHeight.Value})";
        }

        var message = $"Prompt: {prompt}\n\n" +
            $"Negative Prompt: {negativePrompt}\n\n" +
            $"Steps: {settings.Steps}, Sampler: {settings.Sampler}\n" +
            $"Guidance Scale (Cfg): {settings.GuidanceScale}\n" +
            $"Seed: {settings.Seed}\n" +
            $"Size: {size}\n" +
            $"Denoising Strength: {settings.DenoisingStrength}\n" +
            $"Model: {settings.Model?.DisplayName ?? "Unknown"}";

        if (!string.IsNullOrEmpty(settings.Scheduler))
        {
            message += $"\nScheduler: {settings.Scheduler}";
        }

        if (settings.DistilledCfgScale.HasValue)
        {
            message += $"\nDistilled CFG Scale: {settings.DistilledCfgScale}";
        }

        if (settings.EnableUpscaling && !string.IsNullOrEmpty(settings.Upscaler))
        {
            message += $"\nUpscaler: {settings.Upscaler}";

            if (settings.UpscaleLevel > 0)
            {
                message += $"\nUpscale Level: {settings.UpscaleLevel}";
            }

            if (settings.UpscaleSteps > 0)
            {
                message += $"\nUpscale Steps: {settings.UpscaleSteps}";
            }
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