using CommunityToolkit.Maui.Views;
using MobileDiffusion.Helpers;
using MobileDiffusion.Models;

namespace MobileDiffusion.Views.Popups;

public partial class HistoryItemPopup : BasePopup
{
	public HistoryItemPopup()
	{
        InitializeComponent();

        var popupSizeConstants = ServiceHelper.GetService<PopupSizeConstants>();
        
        MainGrid.WidthRequest = popupSizeConstants.ExtraLarge.Width;
        MainGrid.HeightRequest = popupSizeConstants.ExtraLarge.Height;

        var minDimension = Math.Min(MainGrid.WidthRequest, MainGrid.HeightRequest);

        ImageControl.WidthRequest = minDimension;
        ImageControl.HeightRequest = minDimension;
    }
}