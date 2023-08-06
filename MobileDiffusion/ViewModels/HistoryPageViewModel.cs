using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Input;

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

    [ObservableProperty]
    private IList<Object> _selectedItems;

    [ObservableProperty]
    private bool _selectionModeEnabled;

    [ObservableProperty]
    private string _selectedItemsText;

    [ObservableProperty]
    private bool _isLoading = true;

    public ICommand HideBottomPanelCommand { get; set; }

    public ICommand ShowBottomPanelCommand { get; set; }

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

                //// REMOVE ALL THUMBNAILS - REMOVE THIS AFTER TESTING
                //foreach (var file in allFiles.Where(s => Path.GetFileName(s).StartsWith(Constants.ThumbnailPrefix)).ToArray())
                //{
                //    File.Delete(file);
                //}

                await Shell.Current.Dispatcher.DispatchAsync(LoadItems);
            }
            else
            {
                IsLoading = false;
            }
        });
    }

    [RelayCommand]
    private async Task ClearHistory()
    {
        var result = await Shell.Current.DisplayAlert("Confirm", "Are you sure you would like to clear all of your history?\n\n**This cannot be undone.**", "CLEAR ALL", "Cancel");

        if (!result)
        {
            return;
        }

        foreach(var filePath in _allImageFileNames)
        {
            await _fileService.DeleteFileFromInternalStorage(filePath);
        }

        HistoryItems.Clear();
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

    [RelayCommand]
    private void ToggleSelectionMode()
    {
        SelectionModeEnabled = !SelectionModeEnabled;
        
        if (!SelectionModeEnabled)
        {
            HideBottomPanelCommand?.Execute(null);
            SelectedItems.Clear();
        }
        else
        {
            ShowBottomPanelCommand?.Execute(null);
        }


        SelectionChanged();
    }

    [RelayCommand]
    private void SelectionChanged()
    {
        var pluralityString = SelectedItems.Count != 1 ? "s" : string.Empty;
        SelectedItemsText = $"{SelectedItems.Count} item{pluralityString} selected";
    }

    [RelayCommand]
    private async Task DeleteSelectedItems()
    {
        if (SelectedItems.Count == 0)
        {
            return;
        }

        var pluralityString = SelectedItems.Count != 1 ? "s" : string.Empty;
        SelectedItemsText = $"{SelectedItems.Count} item{pluralityString} selected";

        var result = await Shell.Current.DisplayAlert("Confirm", $"Delete {SelectedItems.Count} item{pluralityString}?", "DELETE", "Cancel");

        if (!result)
        {
            return;
        }

        foreach (var item in SelectedItems)
        {
            if (item is IHistoryItemViewModel viewModel)
            {
                await _fileService.DeleteFileFromInternalStorage(viewModel.ThumbnailFileName);
                await _fileService.DeleteFileFromInternalStorage(viewModel.FileName);

                HistoryItems.Remove(viewModel);
            }
        }

        SelectedItems.Clear();
    }
}
