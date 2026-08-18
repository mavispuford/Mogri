namespace Mogri;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

#if IOS26_0_OR_GREATER
        this.SetValue(Shell.TabBarBackgroundColorProperty, Colors.Transparent);
#endif
    }
}
