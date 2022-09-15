namespace MobileDiffusion.Controls;

public partial class ImageResultControl : ContentView
{
    public static BindableProperty IsLoadingProperty = BindableProperty.Create(nameof(IsLoading),
        typeof(bool), typeof(ImageResultControl), true, propertyChanged: (bindable, oldValue, newValue) =>
        {
            ((ImageResultControl)bindable).onIsLoadingChanged();
        });

    public static BindableProperty SourceProperty = BindableProperty.Create(nameof(Source),
        typeof(ImageSource), typeof(ImageResultControl), default(ImageSource), propertyChanged: (bindable, oldValue, newValue) =>
        {
            ((ImageResultControl)bindable).onImageSourceChanged();
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

    private void onIsLoadingChanged()
    {
        if (!IsLoading && ImageControl.Scale == 0)
        {
            ImageControl.ScaleTo(1, 250u, Easing.CubicInOut);
        }
    }

	private void onImageSourceChanged()
	{
        if (Source != null)
        {
            ImageControl.Source = Source;
        }
    }
}