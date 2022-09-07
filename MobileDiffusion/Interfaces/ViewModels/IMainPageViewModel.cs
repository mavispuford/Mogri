using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IMainPageViewModel : IBaseViewModel
{
    string Prompt { get; set; }
    string PlaceholderPrompt { get; set; }
    ObservableCollection<IResultItemViewModel> Results { get; set; }
    IAsyncRelayCommand CreateCommand { get; }
    IAsyncRelayCommand ShowImageToImageSettingsCommand { get; }
    IAsyncRelayCommand ShowRequestSettingsCommand { get; }
}