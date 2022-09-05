using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public class PageViewModel : BaseViewModel, IPageViewModel
{
    public virtual void ApplyQueryAttributes(IDictionary<string, object> query)
    {
    }
}
