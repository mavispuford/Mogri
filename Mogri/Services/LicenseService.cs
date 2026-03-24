using Mogri.Interfaces.Services;
using Mogri.Models;
using Newtonsoft.Json;

namespace Mogri.Services;

/// <summary>
///     Implementation of ILicenseService that loads license data from an embedded JSON file.
/// </summary>
public class LicenseService : ILicenseService
{
    private IReadOnlyList<LicenseEntry>? _cachedLicenses;

    /// <summary>
    ///     Loads and returns all open source license entries.
    /// </summary>
    /// <returns>A read-only list of license entries.</returns>
    public async Task<IReadOnlyList<LicenseEntry>> GetLicensesAsync()
    {
        if (_cachedLicenses != null)
        {
            return _cachedLicenses;
        }

        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("licenses.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            
            var entries = JsonConvert.DeserializeObject<List<LicenseEntry>>(json);
            _cachedLicenses = entries ?? new List<LicenseEntry>();
        }
        catch (Exception ex)
        {
            // Handle parsing or reading errors gracefully
            System.Diagnostics.Debug.WriteLine($"Failed to load licenses: {ex.Message}");
            _cachedLicenses = new List<LicenseEntry>();
        }

        return _cachedLicenses;
    }
}
