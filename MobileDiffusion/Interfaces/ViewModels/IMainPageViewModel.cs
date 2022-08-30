using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IMainPageViewModel : IBaseViewModel
{
    string Prompt { get; set; }
    string PlaceholderPrompt { get; set; }

    List<ImageSource> ResultImageSources { get; set; }
    IAsyncRelayCommand CreateCommand { get; }
}