using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IMainPageViewModel : IBaseViewModel
{
    string Prompt { get; set; }
    string PlaceholderPrompt { get; set; }
    double ImageLayoutWidth { get; set; }
    double MainLayoutWidth { get; set; }
    Thickness MainLayoutPadding { get; set; }
    double ImageWidth { get; set; }
    double ImageHeight { get; set; }
    ObservableCollection<ImageSource> ResultImageSources { get; set; }
    IAsyncRelayCommand CreateCommand { get; }
    IAsyncRelayCommand ShowRequestSettingsCommand { get; }
}