namespace MobileDiffusion.Controls;

public partial class ToolControl : ContentView
{
	public Color IconColor
	{
		get => (Color)GetValue(IconColorProperty);
		set => SetValue(IconColorProperty, value);
	}

	public static readonly BindableProperty IconColorProperty = BindableProperty.Create(nameof(IconColor), typeof(Color), typeof(ToolControl), Colors.Black, propertyChanged: (bindable, oldValue, newValue) =>
	{
		((ToolControl)bindable).OnIconColorChanged();
    });

	public ToolControl()
	{
		InitializeComponent();
	}

	private void OnIconColorChanged()
	{
		if (IconColor == null) 
		{
			return;
		}

		FontImageSource.Color = IconColor;
    }
}