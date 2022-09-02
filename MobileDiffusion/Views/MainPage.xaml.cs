using CommunityToolkit.Maui.Views;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Views;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

        MainGrid.SetBinding(PaddingProperty, nameof(IMainPageViewModel.MainLayoutPadding), BindingMode.OneWayToSource);
        MainGrid.SetBinding(WidthProperty, nameof(IMainPageViewModel.MainLayoutWidth), BindingMode.OneWayToSource);
	}
}