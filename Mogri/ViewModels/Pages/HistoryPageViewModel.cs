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
    private readonly IHapticsService _hapticsService;
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
    private DateTime _lastPullDownTimestamp = DateTime.MinValue;
    private const int PullDownWindowSeconds = 2;
    #if ANDROID
    private const int RefreshAnimationDelayMilliseconds = 0;
    #else
    private const int RefreshAnimationDelayMilliseconds = 500;
    #endif
    // Null means the gesture does not request a mode change; otherwise this is the mode to apply after release.
    private bool? _pendingVaultMode;
    // The native RefreshView state can lag behind the command on Android, so track the logical request separately.
    private bool _refreshRequested;
    // Delayed refresh completion must not update a page after navigation has changed its state.
    private int _navigationVersion;
    private bool _reloadOnNextNavigation;

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
    [NotifyPropertyChangedFor(nameof(SearchPlaceholderText))]
    [NotifyPropertyChangedFor(nameof(EmptyViewText))]
    public partial bool IsVaultMode { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    public string SearchPlaceholderText => IsVaultMode ? "Search vault..." : "Search history...";

    public string EmptyViewText => IsVaultMode ? "Vault is empty." : "No items found.";

    private CancellationTokenSource? _searchDebounceCts;

    partial void OnSearchTextChanged(string? value)
    {
        var searchVersion = ++_searchVersion;
        clearSelection();

        cancelSearchDebounce();
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
                    if (token.IsCancellationRequested || searchVersion != _searchVersion) return;

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

    private void cancelSearchDebounce()
    {
        if (_searchDebounceCts == null)
        {
            return;
        }

        _searchDebounceCts.Cancel();
        _searchDebounceCts.Dispose();
        _searchDebounceCts = null;
    }

    public ICommand? HideBottomPanelCommand { get; set; }

    public ICommand? ShowBottomPanelCommand { get; set; }

    public HistoryPageViewModel(IFileService fileService,
        IImageService imageService,
        IHistoryService historyService,
        IServiceProvider serviceProvider,
        IPopupService popupService,
        IToastService toastService,
        IHapticsService hapticsService,
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
        _hapticsService = hapticsService ?? throw new ArgumentNullException(nameof(hapticsService));
        _mainThreadService = mainThreadService ?? throw new ArgumentNullException(nameof(mainThreadService));
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        _navigationVersion++;
        var shouldReload = _reloadOnNextNavigation || IsVaultMode;
        _reloadOnNextNavigation = false;
        IsVaultMode = false;
        _pendingVaultMode = null;
        _refreshRequested = false;
        _lastPullDownTimestamp = DateTime.MinValue;
        cancelSearchDebounce();
        _isInitialized = false;

        _ = Task.Run(async () =>
        {
            try
            {
                var hasChanges = await _historyService.InitializeAsync();

                await _mainThreadService.InvokeOnMainThreadAsync(async () =>
                {
                    if (shouldReload || hasChanges || HistoryItems.Count == 0 || !string.IsNullOrWhiteSpace(SearchText))
                    {
                        resetSelectionMode();
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

    public override async Task OnNavigatedFromAsync()
    {
        _navigationVersion++;
        _reloadOnNextNavigation = IsVaultMode;
        IsVaultMode = false;
        IsRefreshing = false;
        _pendingVaultMode = null;
        _refreshRequested = false;
        _lastPullDownTimestamp = DateTime.MinValue;
        cancelSearchDebounce();

        await base.OnNavigatedFromAsync();
    }

    [RelayCommand]
    private async Task PullDownGesture()
    {
        _refreshRequested = true;
        IsRefreshing = true;

        if (SelectionModeEnabled)
        {
            return;
        }

        if (IsVaultMode)
        {
            _lastPullDownTimestamp = DateTime.MinValue;
            _pendingVaultMode = false;
            _hapticsService.Perform(Enums.HapticType.Click);
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastPullDownTimestamp).TotalSeconds <= PullDownWindowSeconds)
        {
            _lastPullDownTimestamp = DateTime.MinValue;
            _pendingVaultMode = true;
            _hapticsService.Perform(Enums.HapticType.LongPress);
            return;
        }

        _pendingVaultMode = null;
        _lastPullDownTimestamp = now;
        _hapticsService.Perform(Enums.HapticType.Click);
        await _toastService.ShowAsync("Swipe down again to open vault");
    }

    [RelayCommand]
    private async Task RefreshEnded()
    {
        // The touch behavior also reports ordinary scrolling, so use the command signal instead of sampling IsRefreshing.
        if (!_refreshRequested)
        {
            return;
        }

        var navigationVersion = _navigationVersion;
        var pendingVaultMode = _pendingVaultMode;
        _refreshRequested = false;
        _pendingVaultMode = null;

        // Let the native refresh control settle before changing the collection contents.
        await Task.Delay(RefreshAnimationDelayMilliseconds);

        if (navigationVersion != _navigationVersion)
        {
            return;
        }

        IsRefreshing = false;

        if (pendingVaultMode.HasValue)
        {
            await reloadHistoryForModeAsync(pendingVaultMode.Value);
        }
    }

    [RelayCommand]
    private async Task ExitVaultMode()
    {
        if (!IsVaultMode)
        {
            return;
        }

        _lastPullDownTimestamp = DateTime.MinValue;
        await reloadHistoryForModeAsync(false);
    }

    private async Task reloadHistoryForModeAsync(bool isVaultMode)
    {
        cancelSearchDebounce();
        SearchText = string.Empty;
        IsVaultMode = isVaultMode;

        await _semaphore.WaitAsync();
        try
        {
            itemIndex = 0;
            HistoryItems.Clear();
            _loadedEntities.Clear();
            resetSelectionMode();

            if (_isInitialized)
            {
                await LoadItemsCoreAsync(initialLoadTakeCount);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    [RelayCommand]
    private async Task ClearHistory()
    {
        var result = await _popupService.DisplayAlertAsync("Confirm", "Are you sure you would like to clear all of your history?\n\n**This cannot be undone.**", "CLEAR ALL", "Cancel");

        if (!result)
        {
            return;
        }

        var allItems = await _historyService.SearchAsync(string.Empty, 0, int.MaxValue, isHidden: IsVaultMode);

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
        var results = (await _historyService.SearchAsync(SearchText ?? string.Empty, itemIndex, takeCount, isHidden: IsVaultMode) ?? []).ToList();
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
                results = (await _historyService.SearchAsync(query, 0, int.MaxValue, isHidden: IsVaultMode) ?? []).ToList();
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

    private void resetSelectionMode()
    {
        if (SelectionModeEnabled)
        {
            SelectionModeEnabled = false;
            HideBottomPanelCommand?.Execute(null);
        }

        clearSelection();
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
    private Task HideSelectedItems()
    {
        return setSelectedItemsHiddenAsync(true);
    }

    [RelayCommand]
    private Task UnhideSelectedItems()
    {
        return setSelectedItemsHiddenAsync(false);
    }

    private async Task setSelectedItemsHiddenAsync(bool isHidden)
    {
        if (SelectedItems == null)
        {
            return;
        }

        synchronizeSelectedEntities();
        var entities = _selectedEntities.Values.ToList();
        if (entities.Count == 0)
        {
            return;
        }

        try
        {
            await _historyService.SetItemsHiddenAsync(entities, isHidden);
            await RemoveDeletedEntitiesAndBackfillAsync(entities);
            resetSelectionMode();

            var message = isHidden
                ? $"{entities.Count} item(s) moved to vault"
                : $"{entities.Count} item(s) restored to history";
            await _toastService.ShowAsync(message);
        }
        catch (Exception ex)
        {
            var action = isHidden ? "move items to vault" : "restore items to history";
            await _toastService.ShowAsync($"Failed to {action}: {ex.Message}");
        }
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