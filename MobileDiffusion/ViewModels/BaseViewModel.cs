using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

[INotifyPropertyChanged]
public partial class BaseViewModel : IBaseViewModel
{
    public virtual void ApplyQueryAttributes(IDictionary<string, object> query)
    {
    }
}
