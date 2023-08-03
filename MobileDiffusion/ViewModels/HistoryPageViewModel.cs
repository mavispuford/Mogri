using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using System.Collections.ObjectModel;
using System.IO;

namespace MobileDiffusion.ViewModels;

public partial class HistoryPageViewModel : PageViewModel, IHistoryPageViewModel
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly IFileService _fileService;
    private readonly IImageService _imageService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPopupService _popupService;

    private string[] _allImageFileNames;

    private int itemIndex = 0;
    private const int itemTakeCount = 12;

    [ObservableProperty]
    private ObservableCollection<IHistoryItemViewModel> _historyItems = new();

    public HistoryPageViewModel(IFileService fileService,
        IImageService imageService,
        IServiceProvider serviceProvider,
        IPopupService popupService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        _ = Task.Run(async () =>
        {
            // ALL FILES INCLUDING THUMBNAILS
            var allFiles = await _fileService.GetFileListFromInternalStorageAsync();
            
            if (allFiles != null)
            {
                // Non-thumbnail files only
                _allImageFileNames = allFiles.Where(s => !Path.GetFileName(s).StartsWith(Constants.ThumbnailPrefix)).ToArray();

                // REMOVE ALL THUMBNAILS - REMOVE THIS AFTER TESTING
                foreach (var file in allFiles.Where(s => Path.GetFileName(s).StartsWith(Constants.ThumbnailPrefix)).ToArray())
                {
                    File.Delete(file);
                }

                await Shell.Current.Dispatcher.DispatchAsync(LoadItems);
            }
        });
    }

    [RelayCommand]
    private async Task ItemTapped(IHistoryItemViewModel item)
    {
        var popupParameters = new Dictionary<string, object>
        {
            { NavigationParams.HistoryItem, item }
        };

        var result = (await _popupService.ShowPopupAsync("HistoryItemPopup", popupParameters)) as Dictionary<string, object>;

        if (result == null)
        {
            return;
        }

        if (result.ContainsKey(NavigationParams.PromptSettings) ||
            result.ContainsKey(NavigationParams.InitImgString) ||
            result.ContainsKey(NavigationParams.CanvasImageString))
        {
            await Shell.Current.GoToAsync("..", result);
        }

        if (result.ContainsKey(NavigationParams.DeletedHistoryItem))
        {
            HistoryItems.Remove(item);
        }
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
            foreach (var filename in _allImageFileNames.Take(new Range(itemIndex, itemIndex + itemTakeCount)))
            {
                var historyItem = _serviceProvider.GetService<IHistoryItemViewModel>();

                HistoryItems.Add(historyItem);

                _ = Task.Run(() => historyItem.InitWith(filename, _fileService, _imageService));
            }

            itemIndex += itemTakeCount;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
