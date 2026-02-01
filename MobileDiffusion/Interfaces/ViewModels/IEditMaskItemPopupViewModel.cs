using MobileDiffusion.Interfaces.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IEditMaskItemPopupViewModel : IPopupBaseViewModel
{
    bool IsDragging { get; set; }
    string DragInfoText { get; set; }
    double BrushSize { get; set; }
    double Noise { get; set; }
    float Alpha { get; set; }
    Color DisplayColor { get; set; }
    bool IsBrush { get; set; }
    bool IsColorVisible { get; }
    bool IsNoiseVisible { get; }

    IRelayCommand DragStartedCommand { get; }
    IRelayCommand DragCompletedCommand { get; }
    IAsyncRelayCommand ChangeColorCommand { get; }
}
