using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Mogri.Interfaces.ViewModels.Pages;

public interface IHistoryPageViewModel : IPageViewModel
{
    bool IsLoading { get; set; }

    bool IsVaultMode { get; set; }

    bool IsRefreshing { get; set; }

    string SearchPlaceholderText { get; }

    string EmptyViewText { get; }

    ObservableCollection<IHistoryItemViewModel> HistoryItems { get; set; }

    IList<object>? SelectedItems { get; set; }

    IAsyncRelayCommand<IHistoryItemViewModel> ItemTappedCommand { get; }

    string? SelectedItemsText { get; set; }

    bool SelectionModeEnabled { get; set; }

    IAsyncRelayCommand ClearHistoryCommand { get; }

    IAsyncRelayCommand DeleteSelectedItemsCommand { get; }

    IRelayCommand<IHistoryItemViewModel> ItemLongPressedCommand { get; }

    IAsyncRelayCommand SelectAllResultsCommand { get; }

    IAsyncRelayCommand LoadItemsCommand { get; }

    IAsyncRelayCommand PullDownGestureCommand { get; }

    IAsyncRelayCommand RefreshEndedCommand { get; }

    IAsyncRelayCommand ExitVaultModeCommand { get; }

    IAsyncRelayCommand HideSelectedItemsCommand { get; }

    IAsyncRelayCommand UnhideSelectedItemsCommand { get; }

    IRelayCommand ToggleSelectionModeCommand { get; }

    IRelayCommand<SelectionChangedEventArgs> SelectionChangedCommand { get; }

    ICommand? HideBottomPanelCommand { get; set; }

    ICommand? ShowBottomPanelCommand { get; set; }

    string? SearchText { get; set; }
}
