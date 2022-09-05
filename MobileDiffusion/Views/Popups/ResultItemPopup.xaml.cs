using CommunityToolkit.Maui.Views;
using MobileDiffusion.Helpers;
using MobileDiffusion.Models;

namespace MobileDiffusion.Views.Popups;

public partial class ResultItemPopup : BasePopup
{
	public ResultItemPopup()
	{
        InitializeComponent();

        var popupSizeConstants = ServiceHelper.GetService<PopupSizeConstants>();
        
        MainGrid.WidthRequest = popupSizeConstants.Large.Width;
        MainGrid.HeightRequest = popupSizeConstants.Large.Height;

        var minDimension = Math.Min(MainGrid.WidthRequest, MainGrid.HeightRequest);

        ImageControl.WidthRequest = minDimension;
        ImageControl.HeightRequest = minDimension;
    }
}