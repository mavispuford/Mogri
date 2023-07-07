using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class ResultItemPopupViewModel : PopupBaseViewModel, IResultItemPopupViewModel
{
    private readonly IFileService _fileService;

    [ObservableProperty]
    private IResultItemViewModel resultItem;

    public ResultItemPopupViewModel(
        IPopupService popupService,
        IFileService fileService) : base(popupService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.ImageResultItem, out var imageResultParam) &&
            imageResultParam is IResultItemViewModel imageResultItem)
        {
            ResultItem = imageResultItem;
        }
        else
        {
            ClosePopup();
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    [RelayCommand]
    private void Close()
    {
        ClosePopup();
    }

    [RelayCommand]
    private async Task Save()
    {
        var stream = await _fileService.GetFileStreamFromInternalStorageAsync(resultItem.InternalUri);
        await _fileService.WriteImageFileToExternalStorageAsync(Path.GetFileName(resultItem.InternalUri), stream);

        await Toast.Make("Image saved.").Show();
    }

    [RelayCommand]
    private void UseSeed()
    {
        ResultItem.Settings.NumOutputs = 1;

        var parameters = new Dictionary<string, object>
        {
            { NavigationParams.PromptSettings, ResultItem.Settings }
        };

        ClosePopup(parameters);
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
                var stream = await _fileService.GetFileStreamFromInternalStorageAsync(resultItem.InternalUri);

                stream.CopyTo(memoryStream);
                var imageBytes = memoryStream.ToArray();
                var imageString = Convert.ToBase64String(imageBytes);

                var parameters = new Dictionary<string, object>
                {
                    { parameterName, asFormattedString ? string.Format(Constants.ImageDataFormat, "image/png", imageString) : imageString }
                };

                ClosePopup(parameters);
            }
        }
        catch
        {
            // TODO - Handle exceptions
        }
    }
}
