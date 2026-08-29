using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using Mogri.Helpers;
using Mogri.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Maui.Core;

namespace Mogri.ViewModels;

public partial class HistoryPageViewModel : PageViewModel, IHistoryPageViewModel
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly SemaphoreSlim _thumbnailLoadSemaphore = new(2, 2);

    private readonly IFileService _fileService;
    private readonly IImageService _imageService;
    private readonly IHistoryService _historyService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPopupService _popupService;
    private readonly IToastService _toastService;
    private readonly IMainThreadService _mainThreadService;

    private int itemIndex = 0;
    private const int itemTakeCount = 30;
    private const int trailingPrefetchCount = 18;
    private const int initialLoadTakeCount = itemTakeCount + trailingPrefetchCount;
    private bool _isInitialized = false;
    private int _lastSelectionCount = 0;
    private readonly Dictionary<IHistoryItemViewModel, HistoryEntity> _loadedEntities = new();
    private readonly Dictionary<string, HistoryEntity> _selectedEntities = new(StringComparer.Ordinal);
    private bool _isSynchronizingSelection;
    private int _searchVersion;

    [ObservableProperty]
    public partial ObservableCollection<IHistoryItemViewModel> HistoryItems { get; set; } = new ObservableRangeCollection<IHistoryItemViewModel>();

    [ObservableProperty]
    public partial IList<Object>? SelectedItems { get; set; } = new List<object>();

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
        var searchVersion = ++_searchVersion;
        clearSelection();

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
            if (t.IsCanceled || token.IsCancellationRequested) return;

            await _mainThreadService.InvokeOnMainThreadAsync(async () =>
            {
                if (token.IsCancellationRequested || searchVersion != _searchVersion || !_isInitialized) return;

                try
                {
                    await _semaphore.WaitAsync(token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    if (token.IsCancellationRequested) return;

                    itemIndex = 0;
                    HistoryItems.Clear();
                    _loadedEntities.Clear();
                    await LoadItemsCoreAsync(initialLoadTakeCount);
                }
                finally
                {
                    _semaphore.Release();
                }
            });
        });
    }

    partial void OnSelectedItemsChanged(IList<Object>? value)
    {
        updateSelectionStates();
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

                await _mainThreadService.InvokeOnMainThreadAsync(async () =>
                {
                    if (hasChanges || HistoryItems.Count == 0 || !string.IsNullOrWhiteSpace(SearchText))
                    {
                        clearSelection();
                        itemIndex = 0;
                        HistoryItems.Clear();
                        _loadedEntities.Clear();
                        _isInitialized = true; // Set to true before calling LoadItems
                        await LoadItems();
                    }
                    else
                    {
                        _isInitialized = true;
                    }
                });
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

        await _semaphore.WaitAsync();
        try
        {
            itemIndex = 0;
            HistoryItems.Clear();
            _loadedEntities.Clear();
            clearSelection();
        }
        finally
        {
            _semaphore.Release();
        }
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
        var results = (await _historyService.SearchAsync(SearchText ?? string.Empty, itemIndex, takeCount) ?? []).ToList();
        var newItems = new List<(IHistoryItemViewModel ViewModel, HistoryEntity Entity)>();

        foreach (var entity in results)
        {
            var historyItem = _serviceProvider.GetService<IHistoryItemViewModel>();
            if (historyItem != null)
            {
                newItems.Add((historyItem, entity));
            }
        }

        if (HistoryItems is ObservableRangeCollection<IHistoryItemViewModel> range)
        {
            range.AddRange(newItems.Select(item => item.ViewModel));
        }
        else
        {
            foreach (var item in newItems)
            {
                HistoryItems.Add(item.ViewModel);
            }
        }

        foreach (var item in newItems)
        {
            _loadedEntities[item.ViewModel] = item.Entity;

            if (_selectedEntities.ContainsKey(getEntityKey(item.Entity)))
            {
                addVisibleSelectedItem(item.ViewModel);
            }

            _ = InitializeHistoryItemAsync(item.ViewModel, item.Entity);
        }

        itemIndex += results.Count;
    }

    private async Task InitializeHistoryItemAsync(IHistoryItemViewModel historyItem, HistoryEntity entity)
    {
        await _thumbnailLoadSemaphore.WaitAsync();

        try
        {
            await Task.Run(() => historyItem.InitWith(entity, _fileService, _imageService));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading history thumbnail: {ex}");
        }
        finally
        {
            _thumbnailLoadSemaphore.Release();
        }
    }

    private async Task RemoveDeletedItemsAndBackfillAsync(IEnumerable<IHistoryItemViewModel> deletedItems)
    {
        var removedCount = 0;

        foreach (var deletedItem in deletedItems)
        {
            if (HistoryItems.Remove(deletedItem))
            {
                _loadedEntities.Remove(deletedItem);
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

    private async Task RemoveDeletedEntitiesAndBackfillAsync(IEnumerable<HistoryEntity> deletedEntities)
    {
        var deletedKeys = deletedEntities
            .Select(getEntityKey)
            .ToHashSet(StringComparer.Ordinal);
        var removedCount = 0;

        foreach (var item in HistoryItems.ToList())
        {
            var entity = getHistoryEntity(item);
            if (entity == null || !deletedKeys.Contains(getEntityKey(entity)))
            {
                continue;
            }

            if (HistoryItems.Remove(item))
            {
                _loadedEntities.Remove(item);
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
            clearSelection();
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

        if (_isSynchronizingSelection)
        {
            return;
        }

        synchronizeSelectedEntities();
        updateSelectionStates();

        var selectedCount = _selectedEntities.Count;

        if (_lastSelectionCount != 0 && selectedCount == 0 && SelectionModeEnabled)
        {
            ToggleSelectionMode();
        }

        _lastSelectionCount = selectedCount;

        var pluralityString = selectedCount != 1 ? "s" : string.Empty;
        SelectedItemsText = $"{selectedCount} item{pluralityString} selected";
    }

    private void updateSelectionStates()
    {
        foreach (var item in HistoryItems)
        {
            var entity = getHistoryEntity(item);
            item.IsSelected = entity != null && _selectedEntities.ContainsKey(getEntityKey(entity));
        }
    }

    [RelayCommand]
    private async Task SelectAllResults()
    {
        var query = SearchText ?? string.Empty;
        var searchVersion = _searchVersion;

        try
        {
            List<HistoryEntity> results;

            await _semaphore.WaitAsync();
            try
            {
                results = (await _historyService.SearchAsync(query, 0, int.MaxValue) ?? []).ToList();
            }
            finally
            {
                _semaphore.Release();
            }

            if (searchVersion != _searchVersion)
            {
                return;
            }

            _selectedEntities.Clear();
            foreach (var entity in results)
            {
                _selectedEntities[getEntityKey(entity)] = entity;
            }

            replaceVisibleSelection();
            SelectionChanged(null);
        }
        catch (Exception ex)
        {
            await _toastService.ShowAsync($"Failed to select items: {ex.Message}");
        }
    }

    private void synchronizeSelectedEntities()
    {
        if (SelectedItems == null)
        {
            return;
        }

        var visibleSelectedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var selectedItem in SelectedItems.OfType<IHistoryItemViewModel>())
        {
            var entity = getHistoryEntity(selectedItem);
            if (entity == null)
            {
                continue;
            }

            var key = getEntityKey(entity);
            visibleSelectedKeys.Add(key);
            _selectedEntities[key] = entity;
        }

        foreach (var item in HistoryItems)
        {
            var entity = getHistoryEntity(item);
            if (entity != null && !visibleSelectedKeys.Contains(getEntityKey(entity)))
            {
                _selectedEntities.Remove(getEntityKey(entity));
            }
        }
    }

    private void replaceVisibleSelection()
    {
        var selectedItems = getOrCreateSelectedItems();

        _isSynchronizingSelection = true;
        try
        {
            selectedItems.Clear();

            foreach (var item in HistoryItems)
            {
                var entity = getHistoryEntity(item);
                if (entity != null && _selectedEntities.ContainsKey(getEntityKey(entity)))
                {
                    selectedItems.Add(item);
                }
            }
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }

    private void addVisibleSelectedItem(IHistoryItemViewModel item)
    {
        var selectedItems = getOrCreateSelectedItems();
        if (!selectedItems.Contains(item))
        {
            selectedItems.Add(item);
        }
    }

    private IList<object> getOrCreateSelectedItems()
    {
        if (SelectedItems == null)
        {
            SelectedItems = new List<object>();
        }

        return SelectedItems;
    }

    private void clearSelection()
    {
        _selectedEntities.Clear();

        _isSynchronizingSelection = true;
        try
        {
            SelectedItems?.Clear();
        }
        finally
        {
            _isSynchronizingSelection = false;
        }

        updateSelectionStates();
        _lastSelectionCount = 0;
        SelectedItemsText = "0 items selected";
    }

    private HistoryEntity? getHistoryEntity(IHistoryItemViewModel item)
    {
        if (_loadedEntities.TryGetValue(item, out var entity))
        {
            return entity;
        }

        return item.Entity;
    }

    private static string getEntityKey(HistoryEntity entity)
    {
        return entity.ImageFileName;
    }

    [RelayCommand]
    private async Task DeleteSelectedItems()
    {
        if (SelectedItems == null)
        {
            return;
        }

        synchronizeSelectedEntities();
        var selectedCount = _selectedEntities.Count;
        if (selectedCount == 0)
        {
            return;
        }

        var pluralityString = selectedCount != 1 ? "s" : string.Empty;
        SelectedItemsText = $"{selectedCount} item{pluralityString} selected";

        var result = await _popupService.DisplayAlertAsync("Confirm", $"Delete {selectedCount} item{pluralityString}?", "DELETE", "Cancel");

        if (!result)
        {
            return;
        }

        try
        {
            var entities = _selectedEntities.Values.ToList();

            // Delete from Service (DB + Files)
            await _historyService.DeleteItemsAsync(entities);

            // Update UI
            await RemoveDeletedEntitiesAndBackfillAsync(entities);
            _selectedEntities.Clear();
            clearSelection();
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