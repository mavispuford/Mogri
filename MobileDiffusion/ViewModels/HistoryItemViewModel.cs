using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.ViewModels;

public partial class HistoryItemViewModel : BaseViewModel, IHistoryItemViewModel
{
    [ObservableProperty]
    public partial string FileName { get; set; }

    [ObservableProperty]
    public partial string ThumbnailFileName { get; set; }

    [ObservableProperty]
    public partial ImageSource ThumbnailImageSource { get; set; }

    [ObservableProperty]
    public partial PromptSettings Settings { get; set; }

    [ObservableProperty]
    public partial HistoryEntity Entity { get; set; }

    public async Task InitWith(HistoryEntity entity, IFileService fileService, IImageService imageService)
    {
        Entity = entity;
        FileName = entity.ImageFileName;

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
