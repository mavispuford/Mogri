namespace MobileDiffusion.Interfaces.Services
{
    public interface IFileService
    {
        Task<string> WriteFileToExternalStorageAsync(string fileName, Stream stream);

        //Task<byte[]> GetFileBytesFromExternalStorage(string fileName);
        Task<Stream> GetFileStreamFromExternalStorage(string fileName, string uriString);

    }
}
