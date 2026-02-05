using CommunityToolkit.Mvvm.Input;

namespace MobileDiffusion.Views.Popups;

public partial class ResolutionSelectPopup : BasePopup
{
    public ResolutionSelectPopup()
    {
        InitializeComponent();

        MainBorder.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new RelayCommand(() =>
            {
                AspectRatioEntry.Unfocus();
                AspectRatioEntry.IsEnabled = false;
                AspectRatioEntry.IsEnabled = true;

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
