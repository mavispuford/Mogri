using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Core;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IMaskDrawingPageViewModel : IBaseViewModel
{
    double ImageWidth { get; set; }
    double ImageHeight { get; set; }
    ImageSource ResultImageSource { get; set; }
    ObservableCollection<IDrawingLine> MaskLines { get; set; }
    IAsyncRelayCommand SaveMaskCommand { get; }
}