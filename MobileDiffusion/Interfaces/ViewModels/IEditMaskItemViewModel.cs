using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Models;
using MobileDiffusion.ViewModels;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IEditMaskItemViewModel
{
    string Icon { get; set; }
    Color DisplayColor { get; set; }
    Color ColorWithAlpha { get; set; }
    double Alpha { get; set; }
    CanvasActionViewModel CanvasAction { get; set; }
    IAsyncRelayCommand ChangeColorCommand { get; }
    IRelayCommand DeleteCommand { get; }
    void InitWith(CanvasActionViewModel canvasAction, Action<IEditMaskItemViewModel> deleteAction);
}
