using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using System.Collections.ObjectModel;
using System.IO;

namespace MobileDiffusion.ViewModels;

public partial class HistoryPageViewModel : PageViewModel, IHistoryPageViewModel
{
    private const string ThumbnailPrefix = "t.";
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly IFileService _fileService;
    private readonly IImageService _imageService;

    private string[] _allImageFileNames;

    private int itemIndex = 0;
    private const int itemTakeCount = 12;

    [ObservableProperty]
    private ObservableCollection<ImageSource> _imageSources = new();

    public HistoryPageViewModel(IFileService fileService,
        IImageService imageService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        _allImageFileNames = await _fileService.GetFileListFromInternalStorageAsync();

        if (_allImageFileNames != null)
        {
            // REMOVE ALL THUMBNAILS - REMOVE AFTER TESTING
            foreach (var file in _allImageFileNames.Where(s => Path.GetFileName(s).StartsWith(ThumbnailPrefix)).ToArray())
            {
                File.Delete(file);
            }

            await LoadItems();
        }
    }

    [RelayCommand]
    private async Task ItemTapped(object item)
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task LoadItems()
    {
        if (_allImageFileNames == null || itemIndex >= _allImageFileNames.Length)
        {
            return;
        }

        await _semaphore.WaitAsync();

        try
        {
            await Task.Run(async () =>
            {
                foreach (var filename in _allImageFileNames.Take(new Range(itemIndex, itemIndex + itemTakeCount)))
                {
                    var filenameNoPath = Path.GetFileName(filename);
                    var directoryOnly = filename.Replace(filenameNoPath, string.Empty);
                    var thumbnailFilename = $"{ThumbnailPrefix}{filenameNoPath}";
                    var thumbnailFullPath = Path.Combine(directoryOnly, thumbnailFilename);

                    if (!await _fileService.FileExistsInInternalStorageAsync(thumbnailFullPath))
                    {
                        using var fileStream = await _fileService.GetFileStreamFromInternalStorageAsync(filenameNoPath);
                        var resized = _imageService.GetResizedImageStreamBytes(fileStream, 256, 256, filterImage: true);

                        await _fileService.WriteFileToInternalStorageAsync(thumbnailFullPath, resized.Bytes);
                    }
                    else
                    {
                        // File exists
                    }

                    Shell.Current.Dispatcher.Dispatch(() =>
                    {
                        ImageSources.Add(ImageSource.FromFile(thumbnailFullPath));
                    });
                }
            });

            itemIndex += itemTakeCount;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
