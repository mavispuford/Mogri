using MobileDiffusion.Helpers;
using MobileDiffusion.Models;

namespace MobileDiffusion.Views.Popups;

public partial class ImageToImageSettingsPopup : BasePopup
{
	public ImageToImageSettingsPopup()
	{
        InitializeComponent();

        var popupSizeConstants = ServiceHelper.GetService<PopupSizeConstants>();
        MainGrid.WidthRequest = popupSizeConstants.Large.Width;
    }
}