using MobileDiffusion.Models;

namespace MobileDiffusion.Interfaces.Services;

public interface IFileService
{
    Task<bool> FileExistsInInternalStorageAsync(string filePath);

    Task<Stream> GetFileStreamUsingExactUriAsync(string uriString);

    Task<Stream> GetFileStreamFromExternalStorageAsync(string fileName);

    Task<Stream> GetFileStreamFromInternalStorageAsync(string fileName);

    Task<string[]> GetFileListFromInternalStorageAsync(string path = null);

    Task<Mask> GetMaskFileFromAppDataAsync(string imageFileName);

    Task<string> WriteFileToInternalStorageAsync(string fileName, Stream stream);

    Task<string> WriteFileToInternalStorageAsync(string fileName, byte[] bytes);

    Task<string> WriteMaskFileToAppDataAsync(string imageFileName, Mask mask);

    Task<string> WriteImageFileToExternalStorageAsync(string fileName, Stream stream, bool isMask = false);
}
