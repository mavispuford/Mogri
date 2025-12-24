using System.Windows.Input;

namespace MobileDiffusion.Controls;

public partial class ToolControl : ContentView
{
	public ICommand SelectCommand
	{
        get => (ICommand)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

	public Color IconColor
	{
		get => (Color)GetValue(IconColorProperty);
		set => SetValue(IconColorProperty, value);
	}

	public static readonly BindableProperty IconColorProperty = BindableProperty.Create(nameof(IconColor), typeof(Color), typeof(ToolControl), Colors.Black, propertyChanged: (bindable, oldValue, newValue) =>
	{
		((ToolControl)bindable).OnIconColorChanged();
    });

    public static readonly BindableProperty SelectCommandProperty = BindableProperty.Create(nameof(SelectCommand), typeof(ICommand), typeof(ToolControl), default);

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