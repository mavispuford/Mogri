namespace MobileDiffusion.Interfaces.Services
{
    public interface IFileService
    {
        Task<Stream> GetFileStreamUsingExactUriAsync(string uriString);

        Task<Stream> GetFileStreamFromExternalStorageAsync(string fileName);

        Task<Stream> GetFileStreamFromInternalStorageAsync(string fileName);

        Task<string> WriteFileToInternalStorageAsync(string fileName, Stream stream);

        Task<string> WriteFileToInternalStorageAsync(string fileName, byte[] bytes);

        Task<string> WriteFileToExternalStorageAsync(string fileName, Stream stream);
    }
}
