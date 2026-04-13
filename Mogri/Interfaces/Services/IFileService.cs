using Mogri.ViewModels;

namespace Mogri.Interfaces.Services;

/// <summary>
/// Provides file I/O operations for internal/external storage and mask data persistence.
/// </summary>
public interface IFileService
{
    Task<bool> DeleteFileFromInternalStorageAsync(string filePath);

    Task<bool> FileExistsInInternalStorageAsync(string filePath);

    Task<Stream?> GetFileStreamUsingExactUriAsync(string uriString);

    Task<Stream?> GetFileStreamFromExternalStorageAsync(string fileName);

    Task<Stream?> GetFileStreamFromInternalStorageAsync(string fileName);

    Task<string[]> GetFileListFromInternalStorageAsync(string? path = null);

    Task<MaskViewModel?> GetMaskFileFromAppDataAsync(string imageFileName);

    /// <summary>
    /// Deletes the persisted mask file associated with the given image filename, if one exists.
    /// </summary>
    Task<bool> DeleteMaskFileFromAppDataAsync(string imageFileName);

    Task<string> WriteFileToInternalStorageAsync(string fileName, Stream stream);

    Task<string> WriteFileToInternalStorageAsync(string fileName, byte[] bytes);

    Task<string> WriteMaskFileToAppDataAsync(string imageFileName, MaskViewModel mask);

    Task<string> WriteImageFileToExternalStorageAsync(string fileName, Stream stream, bool isMask = false);

    /// <summary>
    /// Opens a readable stream for the given photo, converting HEIC/HEIF files to JPEG so
    /// SkiaSharp can decode them. Returns the stream and the effective MIME content type.
    /// </summary>
    Task<(Stream? Stream, string ContentType)> OpenNormalizedPhotoStreamAsync(FileResult photo);
}
