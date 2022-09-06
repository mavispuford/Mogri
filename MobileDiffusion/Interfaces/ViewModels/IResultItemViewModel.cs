using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Models.LStein;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IResultItemViewModel : IBaseViewModel
{
    ImageSource ImageSource { get; set; }

    string InternalUri { get; set; }

    LSteinResponseItem ResponseItem { get; set; }

    IAsyncRelayCommand TappedCommand { get; }

    IRelayCommand SetSettingsCommand { get; set; }
}
