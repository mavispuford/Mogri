using System.Windows.Input;

namespace MobileDiffusion.Controls;

public partial class PillControl : ContentView
{

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(PillControl), string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty TooltipTextProperty =
        BindableProperty.Create(nameof(TooltipText), typeof(string), typeof(PillControl), string.Empty);

    public string TooltipText
    {
        get => (string)GetValue(TooltipTextProperty);
        set => SetValue(TooltipTextProperty, value);
    }

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(PillControl), default);

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(PillControl), default);

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public PillControl()
    {
        InitializeComponent();

        _ = Task.Run(async () =>
        {
            await Task.Delay(300);

            await this.ScaleToAsync(1, 250u, Easing.CubicInOut);
        });
    }
}