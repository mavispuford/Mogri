using CommunityToolkit.Maui.Views;
using System.Runtime.CompilerServices;

namespace MobileDiffusion.Views.Popups
{
    public class BasePopup : Popup
    {
        public BasePopup() : base()
        {
            // Global styles don't seem to be working with Popups, so we'll set it up here

            if (!Application.Current.Resources.TryGetValue("White", out var lightBgColor))
            {
                lightBgColor = Colors.White;
            }

            if (!Application.Current.Resources.TryGetValue("Gray950", out var darkBgColor))
            {
                darkBgColor = Colors.Black;
            }

            this.SetAppThemeColor(Popup.ColorProperty, (Color)lightBgColor, (Color)darkBgColor);
        }
    }
}
