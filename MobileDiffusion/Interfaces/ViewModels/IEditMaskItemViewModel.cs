using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Models;
using MobileDiffusion.ViewModels;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IEditMaskItemViewModel
{
    string Icon { get; set; }
    bool IsColorVisible { get; }
    Color DisplayColor { get; set; }
    Color ColorWithAlpha { get; set; }
    double Alpha { get; set; }
    string Description { get; set; }
    CanvasActionViewModel? CanvasAction { get; set; }
    IAsyncRelayCommand EditCommand { get; }
    IRelayCommand DeleteCommand { get; }
    IRelayCommand DuplicateCommand { get; }
    void InitWith(CanvasActionViewModel canvasAction, Action<IEditMaskItemViewModel> deleteAction, Action<IEditMaskItemViewModel> duplicateAction);
}
