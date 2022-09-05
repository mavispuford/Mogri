using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Views;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

        MainGrid.SetBinding(PaddingProperty, nameof(IMainPageViewModel.MainLayoutPadding), BindingMode.OneWayToSource);
        MainGrid.SetBinding(WidthProperty, nameof(IMainPageViewModel.MainLayoutWidth), BindingMode.OneWayToSource);

        ImageLayout.ChildAdded += ImageLayout_ChildAdded;
    }

    private void ImageLayout_ChildAdded(object sender, ElementEventArgs e)
    {
        if (e.Element is VisualElement visualElement)
        {
            visualElement.ScaleTo(1, 250u, Easing.CubicInOut);
        }
    }
}