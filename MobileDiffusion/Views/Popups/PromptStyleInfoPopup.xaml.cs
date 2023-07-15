using MobileDiffusion.Helpers;
using MobileDiffusion.Models;

namespace MobileDiffusion.Views.Popups;

public partial class PromptStyleInfoPopup : BasePopup
{
    public PromptStyleInfoPopup()
    {
        InitializeComponent();

        var popupSizeConstants = ServiceHelper.GetService<PopupSizeConstants>();
        
        MainLayout.WidthRequest = popupSizeConstants.Medium.Width;
        MainLayout.HeightRequest = popupSizeConstants.Tiny.Height;
    }
}