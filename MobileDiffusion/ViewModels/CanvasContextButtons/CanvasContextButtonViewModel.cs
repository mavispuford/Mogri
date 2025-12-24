using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels.CanvasContextButtons;

public partial class CanvasContextButtonViewModel : BaseViewModel
{
    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial ICanvasPageViewModel ParentPage { get; set; }

    public CanvasContextButtonViewModel(ICanvasPageViewModel parentPage)
    {
        ParentPage = parentPage ?? throw new NullReferenceException(nameof(parentPage));
    }

    public virtual void Update()
    {
        IsVisible = ParentPage?.CurrentTool?.ContextButtons?.Contains(this) ?? false;
    }
}
