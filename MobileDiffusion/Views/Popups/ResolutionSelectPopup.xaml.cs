using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Helpers;
using MobileDiffusion.Models;

namespace MobileDiffusion.Views.Popups;

public partial class ResolutionSelectPopup : BasePopup
{
	public ResolutionSelectPopup()
	{
        InitializeComponent();

        var popupSizeConstants = ServiceHelper.GetService<PopupSizeConstants>();

        MainLayout.WidthRequest = popupSizeConstants.ExtraLarge.Width;
        MainLayout.HeightRequest = popupSizeConstants.Medium.Height;

        MainBorder.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new RelayCommand(() =>
            {
                WidthEntry.Unfocus();
                WidthEntry.IsEnabled = false;
                WidthEntry.IsEnabled = true;

                HeightEntry.Unfocus();
                HeightEntry.IsEnabled = false;
                HeightEntry.IsEnabled = true;
            })
        });
    }
}