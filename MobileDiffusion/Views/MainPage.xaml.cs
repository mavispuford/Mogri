using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Views;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

        ImageLayout.ChildAdded += ImageLayout_ChildAdded;
    }

    private void ImageLayout_ChildAdded(object sender, ElementEventArgs e)
    {
        if (e.Element is VisualElement visualElement)
        {
            visualElement.ScaleTo(1, 250u, Easing.CubicInOut);

            var imageLayoutWidth = MainGrid.Width - MainGrid.Padding.HorizontalThickness;

            var imageWidth = ImageLayout.Children.Count switch
            {
                var x when x > 4 => imageLayoutWidth,
                var x when x > 1 && x <= 4 => imageLayoutWidth / 2.5,
                1 => imageLayoutWidth,
            };

            var ratio = visualElement.Width / visualElement.Height;

            var imageHeight = imageWidth / ratio;

            foreach(View view in ImageLayout.Children)
            {
                view.WidthRequest = imageWidth;
                view.HeightRequest = imageHeight;
            }
        }
    }
}