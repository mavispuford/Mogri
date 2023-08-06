using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IHistoryPageViewModel : IPageViewModel
{
    bool IsLoading { get; set; }

    ObservableCollection<IHistoryItemViewModel> HistoryItems { get; set; }

    IList<object> SelectedItems { get; set; }

    IAsyncRelayCommand<IHistoryItemViewModel> ItemTappedCommand { get; }

    string SelectedItemsText { get; set; }

    bool SelectionModeEnabled { get; set; }

    IAsyncRelayCommand ClearHistoryCommand { get; }

    IAsyncRelayCommand DeleteSelectedItemsCommand { get; }

    IAsyncRelayCommand LoadItemsCommand { get; }

    IRelayCommand ToggleSelectionModeCommand { get; }

    IRelayCommand SelectionChangedCommand { get; }
}
