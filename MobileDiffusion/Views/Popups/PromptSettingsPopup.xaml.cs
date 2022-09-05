using CommunityToolkit.Maui.Views;
using MobileDiffusion.Helpers;
using MobileDiffusion.Models;

namespace MobileDiffusion.Views.Popups;

public partial class PromptSettingsPopup : BasePopup
{
	public PromptSettingsPopup()
	{
        InitializeComponent();

        var popupSizeConstants = ServiceHelper.GetService<PopupSizeConstants>();
        MainGrid.WidthRequest = popupSizeConstants.Large.Width;
    }

    private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (sender is Slider slider)
        {
            slider.Value = Math.Round(e.NewValue);
        }
    }
}