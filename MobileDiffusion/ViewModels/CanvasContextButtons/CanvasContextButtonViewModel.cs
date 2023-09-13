using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels.CanvasContextButtons;

public partial class CanvasContextButtonViewModel : BaseViewModel
{
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private ICanvasPageViewModel _parentPage;

    public CanvasContextButtonViewModel(ICanvasPageViewModel parentPage)
    {
        ParentPage = parentPage ?? throw new NullReferenceException(nameof(parentPage));
    }

    public virtual void Update()
    {
        IsVisible = ParentPage?.CurrentTool?.ContextButtons?.Contains(this) ?? false;
    }
}
