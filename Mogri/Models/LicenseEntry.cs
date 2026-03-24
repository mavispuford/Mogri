namespace Mogri.Models;

/// <summary>
///     Represents an open source license entry for libraries and fonts used in the application.
/// </summary>
public class LicenseEntry
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public string LicenseText { get; set; } = string.Empty;
    public string? Url { get; set; }
}
