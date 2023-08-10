using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public class PageViewModel : BaseViewModel, IPageViewModel
{
    public virtual void ApplyQueryAttributes(IDictionary<string, object> query)
    {
    }

    public virtual Task OnAppearingAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnDisappearingAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnNavigatedFromAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnNavigatedToAsync()
    {
        return Task.CompletedTask;
    }

    public virtual bool OnBackButtonPressed()
    {
        return false;
    }
}
