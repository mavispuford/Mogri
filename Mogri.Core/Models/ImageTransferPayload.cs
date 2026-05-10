namespace Mogri.Models;

/// <summary>
/// Represents an encoded image plus the dimensions and thumbnail used for navigation workflows.
/// </summary>
public sealed record ImageTransferPayload(double Width, double Height, string ImageDataString, string? ThumbnailString);