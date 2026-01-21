using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IHistoryItemViewModel : IBaseViewModel
{
    string FileName { get; set; }

    string ThumbnailFileName { get; set; }

    ImageSource ThumbnailImageSource { get; set; }

    PromptSettings Settings { get; set; }

    HistoryEntity Entity { get; set; }

    Task InitWith(HistoryEntity entity, IFileService fileService, IImageService imageService);
}
