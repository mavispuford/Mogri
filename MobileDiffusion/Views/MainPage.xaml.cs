using CommunityToolkit.Maui.Views;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Views;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

		MainGrid.SetBinding(WidthProperty, nameof(IMainPageViewModel.ImageLayoutWidth), BindingMode.OneWayToSource);
	}
}

