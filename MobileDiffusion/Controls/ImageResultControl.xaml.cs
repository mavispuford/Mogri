namespace MobileDiffusion.Controls;

public partial class ImageResultControl : ContentView
{
    public static BindableProperty ImageProperty = BindableProperty.Create(nameof(Image),
        typeof(Image), typeof(ImageResultControl), default(Image));

    public static BindableProperty IsLoadingProperty = BindableProperty.Create(nameof(IsLoading),
        typeof(bool), typeof(ImageResultControl), true, propertyChanged: (bindable, oldValue, newValue) =>
        {
            ((ImageResultControl)bindable).OnIsLoadingChanged();
        });

    public static BindableProperty SourceProperty = BindableProperty.Create(nameof(Source),
        typeof(ImageSource), typeof(ImageResultControl), default(ImageSource), propertyChanged: (bindable, oldValue, newValue) =>
        {
            ((ImageResultControl)bindable).OnImageSourceChanged();
        });
        
    public ImageSource Source
    {
        get => (ImageSource)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Image Image
    {
        get => (Image)GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public ImageResultControl()
    {
        InitializeComponent();

        Image = ImageControl;
    }

    private async void OnIsLoadingChanged()
    {
        if (!IsLoading && ImageControl.Scale == 0)
        {
            // Avoid possible animation hitches caused by image loading
            await Task.Delay(300);

            _ = ImageControl.ScaleToAsync(1, 250u, Easing.CubicInOut);
        }
    }

    private void OnImageSourceChanged()
    {
        if (Source != null)
        {
            ImageControl.Source = Source;
        }
    }
}