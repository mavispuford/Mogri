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
        await _fileService.WriteFileToExternalStorageAsync(Path.GetFileName(resultItem.InternalUri), stream);
    }

    [RelayCommand]
    private void UseSeed()
    {
        var result = Models.Settings.FromResultItem(ResultItem);

        result.NumOutputs = 1;

        ClosePopup(result);
    }

    [RelayCommand]
    private async Task ImageToImage()
    {
        try
        {
            using (var memoryStream = new MemoryStream())
            {
                var stream = await _fileService.GetFileStreamFromInternalStorageAsync(resultItem.InternalUri);

                stream.CopyTo(memoryStream);
                var imageBytes = memoryStream.ToArray();

                var imageString = Convert.ToBase64String(imageBytes);

                ClosePopup(string.Format(Constants.ImageDataFormat, "image/png", imageString));
            }
        }
        catch
        {
            // TODO - Handle exceptions
        }
    }
}
