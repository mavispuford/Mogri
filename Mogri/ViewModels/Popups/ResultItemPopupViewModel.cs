using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;

namespace Mogri.ViewModels;

public partial class ResultItemPopupViewModel : PopupBaseViewModel, IResultItemPopupViewModel
{
    private readonly IFileService _fileService;
    private IList<IResultItemViewModel> _resultItems = [];

    [ObservableProperty]
    public partial IResultItemViewModel ResultItem { get; set; }

    public ResultItemPopupViewModel(
        IPopupService popupService,
        IFileService fileService) : base(popupService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    }

    public override async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.ImageResultItems, out var imageResultItemsParam) &&
            imageResultItemsParam is IList<IResultItemViewModel> imageResultItems)
        {
            _resultItems = imageResultItems;
        }

        if (query.TryGetValue(NavigationParams.ImageResultItem, out var imageResultParam) &&
            imageResultParam is IResultItemViewModel imageResultItem)
        {
            ResultItem = imageResultItem;
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

    [RelayCommand]
    private async Task Close()
    {
        await ClosePopupAsync();
    }

    [RelayCommand]
    private async Task Save()
    {
        if (ResultItem == null) return;

        var stream = await _fileService.GetFileStreamFromInternalStorageAsync(ResultItem.InternalUri);

        if (stream == null) return;

        await _fileService.WriteImageFileToExternalStorageAsync(Path.GetFileName(ResultItem.InternalUri), stream);

        await Toast.Make("Image saved.").Show();
    }

    [RelayCommand]
    private async Task UseSeed()
    {
        if (ResultItem?.Settings == null) return;

        var parameters = new Dictionary<string, object>
        {
            { NavigationParams.Seed, ResultItem.Settings.Seed }
        };

        await ClosePopupAsync(parameters);
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
        if (ResultItem == null) return;

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var stream = await _fileService.GetFileStreamFromInternalStorageAsync(ResultItem.InternalUri);
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

    [RelayCommand]
    private async Task SwipeLeft()
    {
        if (_resultItems == null || ResultItem == null) return;
        var index = _resultItems.IndexOf(ResultItem);
        if (index < _resultItems.Count - 1)
        {
            ResultItem = _resultItems[index + 1];
        }
    }

    [RelayCommand]
    private async Task SwipeRight()
    {
        if (_resultItems == null || ResultItem == null) return;
        var index = _resultItems.IndexOf(ResultItem);
        if (index > 0)
        {
            ResultItem = _resultItems[index - 1];
        }
    }
}
