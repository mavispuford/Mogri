using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.ViewModels.Popups;
using Mopups.Services;

namespace MobileDiffusion.Views.Popups;

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
