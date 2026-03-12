using CommunityToolkit.Mvvm.Input;
using Mogri.Models;

namespace Mogri.Interfaces.ViewModels;

public interface IResultItemViewModel : IBaseViewModel
{
    ImageSource ImageSource { get; set; }

    PromptSettings Settings { get; set; }

    string InternalUri { get; set; }

    ApiResponse ApiResponse { get; set; }

    IList<IResultItemViewModel> ParentCollection { get; set; }

    IAsyncRelayCommand TappedCommand { get; }

    IRelayCommand ApplyQueryParamsFromResultItemCommand { get; set; }

    //IRelayCommand SetSeedCommand { get; set; }

    //IRelayCommand SetSettingsCommand { get; set; }

    //IRelayCommand SetInitImageCommand { get; set; }

    //IRelayCommand SetCanvasImageCommand { get; set; }

    bool IsLoading { get; set; }

    bool Failed { get; set; }
}
