namespace Mogri.Views;

public partial class GenerationSettingsPage : BasePage
{
    public GenerationSettingsPage()
    {
        InitializeComponent();
    }

    private void Slider_ValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (sender is Slider slider)
        {
            slider.Value = Math.Round(e.NewValue);
        }
    }
}
