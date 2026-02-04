namespace MobileDiffusion.Views;

public partial class PromptSettingsPage : BasePage
{
    public PromptSettingsPage()
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
