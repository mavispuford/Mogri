using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IHistoryItemViewModel : IBaseViewModel
{
    string FileName { get; set; }

    string ThumbnailFileName { get; set; }

    ImageSource ThumbnailImageSource { get; set; }

    PromptSettings Settings { get; set; }

    Task InitWith(string fileName, IFileService fileService, IImageService imageService);
}
