using Mogri.Models;

namespace Mogri.Interfaces.Services;

/// <summary>
///     Service for loading open source license data from embedded resources.
/// </summary>
public interface ILicenseService
{
    /// <summary>
    ///     Loads and returns all open source license entries.
    /// </summary>
    /// <returns>A read-only list of license entries.</returns>
    Task<IReadOnlyList<LicenseEntry>> GetLicensesAsync();
}
