namespace MobileDiffusion.Controls;

public partial class ImageResultControl : ContentView
{
    public static BindableProperty IsLoadingProperty = BindableProperty.Create(nameof(IsLoading),
        typeof(bool), typeof(ImageResultControl), true);

    public static BindableProperty SourceProperty = BindableProperty.Create(nameof(Source),
        typeof(ImageSource), typeof(ImageResultControl), default(ImageSource), propertyChanged: (bindable, oldValue, newValue) =>
        {
            ((ImageResultControl)bindable).updateImageSource();
        });
		
	public ImageSource Source
	{
		get => (ImageSource)GetValue(SourceProperty);
		set => SetValue(SourceProperty, value);
	}

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public ImageResultControl()
	{
		InitializeComponent();
	}

	private void updateImageSource()
	{
		if (Source != null)
		{
            ImageControl.Source = Source;

            IsLoading = false;

            ImageControl.ScaleTo(1, 250u, Easing.CubicInOut);
        }
        else
        {
            IsLoading = true;
        }
    }
}