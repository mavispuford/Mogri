using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MobileDiffusion.Interfaces.ViewModels.Pages;

public interface IHistoryPageViewModel : IPageViewModel
{
    bool IsLoading { get; set; }

    ObservableCollection<IHistoryItemViewModel> HistoryItems { get; set; }

    IList<object>? SelectedItems { get; set; }

    IAsyncRelayCommand<IHistoryItemViewModel> ItemTappedCommand { get; }

    string? SelectedItemsText { get; set; }

    bool SelectionModeEnabled { get; set; }

    IAsyncRelayCommand ClearHistoryCommand { get; }

    IAsyncRelayCommand DeleteSelectedItemsCommand { get; }

    IRelayCommand<IHistoryItemViewModel> ItemLongPressedCommand { get; }

    IRelayCommand SelectAllResultsCommand { get; }

    IAsyncRelayCommand LoadItemsCommand { get; }

    IRelayCommand ToggleSelectionModeCommand { get; }

    IRelayCommand<SelectionChangedEventArgs> SelectionChangedCommand { get; }

    ICommand? HideBottomPanelCommand { get; set; }

    ICommand? ShowBottomPanelCommand { get; set; }

    string? SearchText { get; set; }
}
