namespace MobileDiffusion.Interfaces.Services
{
    public interface IFileService
    {
        Task WriteFileToExternalStorageAsync(string fileName, Stream stream);

        Stream GetFileStreamFromExternalStorage(string fileName);
    }
}
