namespace MobileDiffusion.Interfaces.Services
{
    public interface IFileService
    {
        Task<Stream> GetFileStreamUsingExactUri(string uriString);

        Task<Stream> GetFileStreamFromExternalStorage(string fileName);

        Task<Stream> GetFileStreamFromInternalStorage(string fileName);

        Task<string> WriteFileToInternalStorageAsync(string fileName, Stream stream);

        Task<string> WriteFileToInternalStorageAsync(string fileName, byte[] bytes);

        Task<string> WriteFileToExternalStorageAsync(string fileName, Stream stream);
    }
}
