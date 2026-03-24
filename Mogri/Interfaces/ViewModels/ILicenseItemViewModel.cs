using CommunityToolkit.Mvvm.Input;

namespace Mogri.Interfaces.ViewModels;

/// <summary>
///     ViewModel representing an individual open source license entry with expand/collapse capability.
/// </summary>
public interface ILicenseItemViewModel
{
    string Name { get; }
    string Description { get; }
    string LicenseType { get; }
    string LicenseText { get; }
    string? Url { get; }
    
    bool IsExpanded { get; set; }
    
    IRelayCommand ToggleExpandedCommand { get; }
    IAsyncRelayCommand OpenUrlCommand { get; }
}
