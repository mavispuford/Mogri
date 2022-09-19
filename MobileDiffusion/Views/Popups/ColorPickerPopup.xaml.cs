using CommunityToolkit.Maui.Views;
using MobileDiffusion.Helpers;
using MobileDiffusion.Models;

namespace MobileDiffusion.Views.Popups;

public partial class ColorPickerPopup : BasePopup
{
	public ColorPickerPopup()
	{
        InitializeComponent();

        var popupSizeConstants = ServiceHelper.GetService<PopupSizeConstants>();

        MainLayout.WidthRequest = popupSizeConstants.Medium.Width;
        //MainLayout.HeightRequest = popupSizeConstants.Medium.Height;
    }
}