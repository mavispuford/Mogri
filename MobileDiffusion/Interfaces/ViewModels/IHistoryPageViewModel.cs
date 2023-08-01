using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IHistoryPageViewModel : IPageViewModel
{
    ObservableCollection<ImageSource> ImageSources { get; set; }

    IAsyncRelayCommand<object> ItemTappedCommand { get; }

    IAsyncRelayCommand LoadItemsCommand { get; }
}
