using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IMainPageViewModel : IPageViewModel
{
    bool HasInitImage { get; set; }
    bool HasPromptDescriptors { get; set; }
    string Prompt { get; set; }
    string NegativePrompt { get; set; }
    float Progress { get; set; }
    string PromptDescriptorsString { get; set; } 
    ObservableCollection<IResultItemViewModel> Results { get; set; }
    IAsyncRelayCommand CreateCommand { get; }
    IAsyncRelayCommand ShowImageToImageSettingsCommand { get; }
    IAsyncRelayCommand ShowPromptSettingsCommand { get; }
    IAsyncRelayCommand ShowAppSettingsCommand { get; }
    IAsyncRelayCommand ShowPromptDescriptorsCommand { get; }
    IAsyncRelayCommand ShowPromptPageCommand { get; }
}