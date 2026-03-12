using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;
using Mopups.Services;

namespace Mogri.Views.Popups;

public partial class EditMasksPopup : BasePopup
{
    public EditMasksPopup()
    {
        InitializeComponent();
    }

    private void OnCloseClicked(object? sender, EventArgs e)
    {
        MopupService.Instance.PopAsync();
    }
}
