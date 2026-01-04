using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IMainPageViewModel : IPageViewModel
{
    bool HasInitImage { get; set; }
    string Prompt { get; set; }
    string NegativePrompt { get; set; }
    float Progress { get; set; }
    bool ServerConnected { get; set; }
    bool IsGenerating { get; }
    ObservableCollection<IResultItemViewModel> Results { get; set; }
    IAsyncRelayCommand CreateCommand { get; }
    IAsyncRelayCommand CancelCommand { get; }
    IAsyncRelayCommand ShowImageToImageSettingsCommand { get; }
    IAsyncRelayCommand ShowPromptSettingsCommand { get; }
    IAsyncRelayCommand ShowAppSettingsCommand { get; }
    IAsyncRelayCommand ShowPromptPageCommand { get; }
    IAsyncRelayCommand ShowHistoryCommand { get; }
}