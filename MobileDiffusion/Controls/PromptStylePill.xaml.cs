namespace MobileDiffusion.Controls;

public partial class PromptStylePill : ContentView
{
    public PromptStylePill()
    {
        InitializeComponent();

        _ = Task.Run(async () =>
        {
            await Task.Delay(300);

            await this.ScaleTo(1, 250u, Easing.CubicInOut);
        });
    }
}