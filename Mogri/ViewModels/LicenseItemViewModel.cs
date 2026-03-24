using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;

namespace Mogri.ViewModels;

/// <summary>
///     Implementation of the license item ViewModel, handling expand/collapse logic and URL navigation.
/// </summary>
public partial class LicenseItemViewModel : BaseViewModel, ILicenseItemViewModel
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LicenseType { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LicenseText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Url { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public void InitWith(LicenseEntry entry)
    {
        Name = entry.Name;
        Description = entry.Description;
        LicenseType = entry.LicenseType;
        LicenseText = entry.LicenseText;
        Url = entry.Url;
        IsExpanded = false;
    }

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    [RelayCommand]
    private async Task OpenUrlAsync()
    {
        if (!string.IsNullOrEmpty(Url))
        {
            try
            {
                await Browser.Default.OpenAsync(Url, BrowserLaunchMode.SystemPreferred);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open URL: {ex.Message}");
            }
        }
    }
}
