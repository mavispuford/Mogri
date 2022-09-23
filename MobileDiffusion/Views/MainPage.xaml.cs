using MobileDiffusion.Controls;

namespace MobileDiffusion.Views;

public partial class MainPage : BasePage
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
                _ => imageLayoutWidth,
            };

            // TODO - Rework this - The width/height is currently -1 every time, making the ratio 1:1
            var ratio = visualElement.Width / visualElement.Height;

            var imageHeight = imageWidth / ratio;

            foreach(ImageResultControl imageResult in ImageLayout.Children)
            {
                if (imageResult.Image != null)
                {
                    // Attempt to work around this bug: https://github.com/dotnet/maui/issues/9712
                    imageResult.Image.WidthRequest = imageWidth;
                    imageResult.Image.HeightRequest = imageHeight;
                }

                imageResult.WidthRequest = imageWidth;
                imageResult.HeightRequest = imageHeight;
            }
        }
    }
}