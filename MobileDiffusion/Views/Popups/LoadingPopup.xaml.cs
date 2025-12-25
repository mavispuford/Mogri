
namespace MobileDiffusion.Views.Popups;

public partial class LoadingPopup : BasePopup, IQueryAttributable
{
	public LoadingPopup()
	{
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        MessageLabel.Text = null;

        if (query == null)
        {
            return;
        }

        if (query.TryGetValue(NavigationParams.LoadingMessage, out var message))
        {
            SetLoadingMessage((string)message);
        }

        query.Clear();
    }

    protected override bool OnBackButtonPressed()
    {
        // Don't allow back button for loading page...
        return true;
    }

    public void SetLoadingMessage(string message)
    {
        MessageLabel.Text = message;
    }
}