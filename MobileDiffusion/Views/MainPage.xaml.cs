using CommunityToolkit.Maui.Views;

namespace MobileDiffusion.Views;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	private async void Button_Clicked(object sender, EventArgs e)
	{
		var test = DrawingView;
        var stream2 = await DrawingView.GetImageStream(DrawingView.Lines, new Size(ImageView.Width, ImageView.Height), Colors.Transparent.AsPaint());

        var fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sig.png");
        
        using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
        {
            stream2.CopyTo(fileStream);
        }

        ResultImageView.Source = ImageSource.FromFile(fileName);
    }
}

