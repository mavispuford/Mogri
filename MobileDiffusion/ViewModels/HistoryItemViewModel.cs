using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.ViewModels;

public partial class HistoryItemViewModel : BaseViewModel, IHistoryItemViewModel
{
    [ObservableProperty]
    private string _fileName;

    [ObservableProperty]
    private string _thumbnailFileName;

    [ObservableProperty]
    private ImageSource _thumbnailImageSource;

    [ObservableProperty]
    private PromptSettings _settings;

    public async Task InitWith(string fileName, IFileService fileService, IImageService imageService)
    {
        FileName = fileName;

        var filenameNoPath = Path.GetFileName(FileName);
        var directoryOnly = FileName.Replace(filenameNoPath, string.Empty);
        var thumbnailFilename = $"{Constants.ThumbnailPrefix}{filenameNoPath}";
        ThumbnailFileName = Path.Combine(directoryOnly, thumbnailFilename);

        if (!await fileService.FileExistsInInternalStorageAsync(ThumbnailFileName))
        {
            using var fileStream = await fileService.GetFileStreamFromInternalStorageAsync(filenameNoPath);
            var resized = imageService.GetResizedImageStreamBytes(fileStream, 256, 256, filterImage: true);

            await fileService.WriteFileToInternalStorageAsync(ThumbnailFileName, resized.Bytes);
        }
        else
        {
            // File exists
        }

        ThumbnailImageSource = ImageSource.FromFile(ThumbnailFileName);
    }
}
