using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Maui.Core;

namespace Mogri.ViewModels;

public partial class HistoryPageViewModel : PageViewModel, IHistoryPageViewModel
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly IFileService _fileService;
    private readonly IImageService _imageService;
    private readonly IHistoryService _historyService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPopupService _popupService;
    private readonly IToastService _toastService;
    private readonly IMainThreadService _mainThreadService;

    private int itemIndex = 0;
    private const int itemTakeCount = 12;
    private const int trailingPrefetchCount = 6;
    private const int initialLoadTakeCount = itemTakeCount + trailingPrefetchCount;
    private bool _isInitialized = false;
    private int _lastSelectionCount = 0;

    [ObservableProperty]
    public partial ObservableCollection<IHistoryItemViewModel> HistoryItems { get; set; } = new();

    [ObservableProperty]
    public partial IList<Object>? SelectedItems { get; set; }

    [ObservableProperty]
    public partial bool SelectionModeEnabled { get; set; }

    [ObservableProperty]
    public partial string? SelectedItemsText { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    private CancellationTokenSource? _searchDebounceCts;

    partial void OnSearchTextChanged(string? value)
    {
        if (_searchDebounceCts != null)
        {
            _searchDebounceCts.Cancel();
            _searchDebounceCts.Dispose();
        }
        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;
        var token = cts.Token;

        Task.Delay(500, token).ContinueWith(async t =>
        {
            if (t.IsCanceled) return;

            if (Application.Current != null)
            {
                await _mainThreadService.InvokeOnMainThreadAsync(async () =>
                {
                    itemIndex = 0;
                    HistoryItems.Clear();
                    if (LoadItemsCommand != null)
                    {
                        await LoadItemsCommand.ExecuteAsync(null);
                    }
                });
            }
        });
    }

    public ICommand? HideBottomPanelCommand { get; set; }

    public ICommand? ShowBottomPanelCommand { get; set; }

    public HistoryPageViewModel(IFileService fileService,
        IImageService imageService,
        IHistoryService historyService,
        IServiceProvider serviceProvider,
        IPopupService popupService,
        IToastService toastService,
        IMainThreadService mainThreadService,
        INavigationService navigationService,
        ILoadingCoordinator loadingCoordinator) : base(loadingCoordinator, navigationService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
        _mainThreadService = mainThreadService ?? throw new ArgumentNullException(nameof(mainThreadService));
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        _isInitialized = false;

        _ = Task.Run(async () =>
        {
            try
            {
                var hasChanges = await _historyService.InitializeAsync();

                if (Application.Current != null)
                {
                    await _mainThreadService.InvokeOnMainThreadAsync(async () =>
                    {
                        if (hasChanges || HistoryItems.Count == 0 || !string.IsNullOrWhiteSpace(SearchText))
                        {
                            itemIndex = 0;
                            HistoryItems.Clear();
                            _isInitialized = true; // Set to true before calling LoadItems
                            await LoadItems();
                        }
                        else
                        {
                            _isInitialized = true;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading history: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        });
    }

    [RelayCommand]
    private async Task ClearHistory()
    {
        var result = await _popupService.DisplayAlertAsync("Confirm", "Are you sure you would like to clear all of your history?\n\n**This cannot be undone.**", "CLEAR ALL", "Cancel");

        if (!result)
        {
            return;
        }

        var allItems = await _historyService.SearchAsync(string.Empty, 0, int.MaxValue);

        await _historyService.DeleteItemsAsync(allItems);

        itemIndex = 0;
        HistoryItems.Clear();
    }

    [RelayCommand]
    private async Task ItemTapped(IHistoryItemViewModel item)
    {
        var popupParameters = new Dictionary<string, object>
        {
            { NavigationParams.HistoryItem, item },
            { NavigationParams.HistoryItems, HistoryItems }
        };

        var result = (await _popupService.ShowPopupForResultAsync("HistoryItemPopup", popupParameters)) as Dictionary<string, object>;

        if (result == null)
        {
            return;
        }

        if (result.ContainsKey(NavigationParams.PromptSettings) ||
            result.ContainsKey(NavigationParams.InitImgString) ||
            result.ContainsKey(NavigationParams.CanvasImageString))
        {
            await NavigationService.GoBackAsync(result);
        }

        if (result.TryGetValue(NavigationParams.DeletedHistoryItem, out var deletedItem) && deletedItem is IHistoryItemViewModel deletedHistoryItem)
        {
            await RemoveDeletedItemsAndBackfillAsync([deletedHistoryItem]);
        }
    }

    [RelayCommand]
    private void ItemLongPressed(IHistoryItemViewModel item)
    {
        if (item == null) return;

        if (SelectionModeEnabled) return;

        SelectionModeEnabled = true;
        ShowBottomPanelCommand?.Execute(null);

        // Select the item that was long pressed
        if (SelectedItems != null && !SelectedItems.Contains(item))
        {
            SelectedItems.Add(item);
        }

        SelectionChanged(null);
    }

    [RelayCommand]
    private async Task LoadItems()
    {
        var takeCount = itemIndex == 0 && HistoryItems.Count == 0
            ? initialLoadTakeCount
            : itemTakeCount;

        await LoadItemsAsync(takeCount);
    }

    private async Task LoadItemsAsync(int takeCount)
    {
        if (!_isInitialized || takeCount <= 0)
        {
            return;
        }

        await _semaphore.WaitAsync();

        try
        {
            await LoadItemsCoreAsync(takeCount);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task LoadItemsCoreAsync(int takeCount)
    {
        var results = (await _historyService.SearchAsync(SearchText ?? string.Empty, itemIndex, takeCount)).ToList();

        foreach (var entity in results)
        {
            var historyItem = _serviceProvider.GetService<IHistoryItemViewModel>();
            if (historyItem != null)
            {
                HistoryItems.Add(historyItem);

                // Fire and forget initialization to keep UI responsive
                _ = Task.Run(() => historyItem.InitWith(entity, _fileService, _imageService));
            }
        }

        itemIndex += results.Count;
    }

    private async Task RemoveDeletedItemsAndBackfillAsync(IEnumerable<IHistoryItemViewModel> deletedItems)
    {
        var removedCount = 0;

        foreach (var deletedItem in deletedItems)
        {
            if (HistoryItems.Remove(deletedItem))
            {
                removedCount++;
            }
        }

        if (removedCount == 0)
        {
            return;
        }

        if (!_isInitialized)
        {
            itemIndex = Math.Max(0, itemIndex - removedCount);
            return;
        }

        await _semaphore.WaitAsync();

        try
        {
            itemIndex = Math.Max(0, itemIndex - removedCount);
            await LoadItemsCoreAsync(removedCount);
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
            SelectedItems?.Clear();
        }
        else
        {
            ShowBottomPanelCommand?.Execute(null);
        }

        _lastSelectionCount = 0;
        SelectionChanged(null);
    }

    [RelayCommand]
    private void SelectionChanged(SelectionChangedEventArgs? args)
    {
        if (SelectedItems == null) return;

        if (_lastSelectionCount != 0 && SelectedItems.Count == 0 && SelectionModeEnabled)
        {
            ToggleSelectionMode();
        }

        _lastSelectionCount = SelectedItems.Count;

        var pluralityString = SelectedItems.Count != 1 ? "s" : string.Empty;
        SelectedItemsText = $"{SelectedItems.Count} item{pluralityString} selected";
    }

    [RelayCommand]
    private void SelectAllResults()
    {
        if (HistoryItems == null || SelectedItems == null) return;

        foreach (var item in HistoryItems)
        {
            if (!SelectedItems.Contains(item))
            {
                SelectedItems.Add(item);
            }
        }
        SelectionChanged(null);
    }

    [RelayCommand]
    private async Task DeleteSelectedItems()
    {
        if (SelectedItems == null || SelectedItems.Count == 0)
        {
            return;
        }

        var pluralityString = SelectedItems.Count != 1 ? "s" : string.Empty;
        SelectedItemsText = $"{SelectedItems.Count} item{pluralityString} selected";

        var result = await _popupService.DisplayAlertAsync("Confirm", $"Delete {SelectedItems.Count} item{pluralityString}?", "DELETE", "Cancel");

        if (!result)
        {
            return;
        }

        try
        {
            var itemsToDelete = SelectedItems.OfType<IHistoryItemViewModel>().ToList();

            // Get entities
            var entities = itemsToDelete.Select(x => x.Entity).Where(x => x != null).ToList();

            // Delete from Service (DB + Files)
            await _historyService.DeleteItemsAsync(entities);

            // Update UI
            await RemoveDeletedItemsAndBackfillAsync(itemsToDelete);
            SelectedItems.Clear();
            SelectionChanged(null);
        }
        catch (Exception ex)
        {
            await _toastService.ShowAsync($"Failed to delete items: {ex.Message}");
        }
    }

    public override bool OnBackButtonPressed()
    {
        if (SelectionModeEnabled)
        {
            ToggleSelectionMode();

            return true;
        }

        return base.OnBackButtonPressed();
    }

}