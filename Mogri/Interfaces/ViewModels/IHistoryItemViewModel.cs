using Mogri.Interfaces.Services;
using Mogri.Models;

namespace Mogri.Interfaces.ViewModels;

public interface IHistoryItemViewModel : IBaseViewModel
{
    string FileName { get; set; }

    string ThumbnailFileName { get; set; }

    ImageSource ThumbnailImageSource { get; set; }

    PromptSettings? Settings { get; set; }

    HistoryEntity Entity { get; set; }

    Task InitWith(HistoryEntity entity, IFileService fileService, IImageService imageService);
}
