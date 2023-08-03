using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IHistoryPageViewModel : IPageViewModel
{
    ObservableCollection<IHistoryItemViewModel> HistoryItems { get; set; }

    IAsyncRelayCommand<IHistoryItemViewModel> ItemTappedCommand { get; }

    IAsyncRelayCommand LoadItemsCommand { get; }
}
