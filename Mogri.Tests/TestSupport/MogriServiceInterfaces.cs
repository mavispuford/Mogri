using Mogri.Interfaces.ViewModels.Popups;
using Mogri.Models;
using SkiaSharp;
using Mogri.Enums;

namespace Mogri.Interfaces.Services;

public interface IFileService
{
    Task<Stream?> GetFileStreamFromInternalStorageAsync(string fileName);

    Task<Stream?> GetFileStreamUsingExactUriAsync(string uriString);

    Task WriteImageFileToExternalStorageAsync(string fileName, Stream stream, bool overwrite = true);

    Task<bool> DeleteFileFromInternalStorageAsync(string filePath);
}

public interface IImageService
{
    SKBitmap? GetSkBitmapFromStream(Stream? stream);

    Task<MemoryStream?> GetStreamFromContentTypeStringAsync(string? imageString, CancellationToken token);

    (byte[]? Bytes, int ActualWidth, int ActualHeight) GetResizedImageStreamBytes(Stream? stream, int width, int height, bool forceExactSize = false, bool filterImage = false, bool onlyIfLarger = false);

    SKBitmap? GetResizedSKBitmap(SKBitmap? sourceBitmap, int maxWidth, int maxHeight, bool filterImage = false, bool onlyIfLarger = false);

    string? GetThumbnailString(SKBitmap? bitmap, string contentType, int width = 256, int height = 256);
}

public interface ISegmentationService
{
    SKColor MaskColor { get; }

    Task<bool> SetImage(SKBitmap bitmap, CancellationToken token);

    Task<SKBitmap?> DoSegmentation(SKPoint[] points, bool reset = false);

    SKBitmap InvertMask(SKBitmap currentMask);

    void Reset();

    void UnloadModel();
}

public interface IPatchService
{
    Task<SKBitmap> PatchImageAsync(SKBitmap image, SKBitmap mask);

    void UnloadModel();
}

public interface IHistoryService
{
    Task<bool> InitializeAsync();

    Task<IList<HistoryEntity>> SearchAsync(string searchText, int skip, int take);

    Task DeleteItemsAsync(IList<HistoryEntity> items);
}

public interface IPopupService
{
    Task<object?> ShowPopupForResultAsync(string name, IDictionary<string, object>? parameters);

    Task ClosePopupAsync(IPopupBaseViewModel viewModel, object? result);

    Task DisplayAlertAsync(string title, string message, string cancel);

    Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel);
}

public interface IToastService
{
    Task ShowAsync(string message);
}

public interface IAnimationService
{
    void AnimateProgress(float start, float end, Action<float> onUpdate);
}

public interface ICheckpointSettingsService
{
    void Save(string checkpointKey, CheckpointSettings settings);

    CheckpointSettings? Load(string checkpointKey);
}

public interface IHapticsService
{
    bool IsSupported { get; }

    void Perform(HapticType type);
}

public interface IMainThreadService
{
    Task InvokeOnMainThreadAsync(Action action);

    Task InvokeOnMainThreadAsync(Func<Task> action);
}

public interface INavigationService
{
    Task GoToAsync(string route);

    Task GoToAsync(string route, IDictionary<string, object> parameters);

    Task GoBackAsync();

    Task GoBackAsync(IDictionary<string, object> parameters);

    Task PopToRootAsync();

    Task PopToRootAndGoToAsync(string route, IDictionary<string, object> parameters);
}
