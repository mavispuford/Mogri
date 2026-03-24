using System.Collections.ObjectModel;
using Mogri.Interfaces.ViewModels;

namespace Mogri.Interfaces.ViewModels.Pages;

/// <summary>
///     ViewModel for the open source licenses page.
/// </summary>
public interface ILicensesPageViewModel : IPageViewModel
{
    ObservableCollection<ILicenseItemViewModel> Licenses { get; }
}
