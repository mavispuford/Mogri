using Android.Content;
using Android.Provider;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Models;
using System.Text.Json;
using System.Threading.Tasks;
using AndroidNet = Android.Net;

namespace MobileDiffusion.Platforms.Android.Services;

public class FileService : IFileService
{
    private const string extFolderName = "Pictures/MobileDiffusion/";

    private const string extFolderNameMasks = "Pictures/MobileDiffusion/Masks/";

    public async Task<Stream> GetFileStreamUsingExactUriAsync(string uriString)
    {
        await checkForReadPermission();

        var contentResolver = Platform.CurrentActivity.ContentResolver;

        try
        {
            var uri = AndroidNet.Uri.Parse(uriString);

            return contentResolver.OpenInputStream(uri);
        }
        catch (Exception e)
        {
            // TODO - Handle exception

            Console.WriteLine($"Error getting requested file \"(uri: {uriString})\"");
        }

        return null;
    }

    public async Task<Stream> GetFileStreamFromExternalStorageAsync(string fileName)
    {
        await checkForReadPermission();

        return await getFileStreamFromStorageUsingBaseUri(fileName, MediaStore.Images.Media.ExternalContentUri);

        //return await getFileStreamFromStorageUsingBaseUri(fileName, AndroidNet.Uri.WithAppendedPath(MediaStore.Images.Media.ExternalContentUri, extFolderName));
    }

    public Task<Stream> GetFileStreamFromInternalStorageAsync(string fileName)
    {
        var fullPath = fileName.Contains(FileSystem.CacheDirectory) ? fileName : Path.Combine(FileSystem.CacheDirectory, fileName);

        try
        {
            var reader = new StreamReader(fullPath, true);

            return Task.FromResult(reader.BaseStream);
        }
        catch (Exception e)
        {
            // TODO - Handle exception

            Console.WriteLine($"Error reading requested file from \"{fullPath}\"");
        }

        return null;
    }

    private Task<Stream> getFileStreamFromStorageUsingBaseUri(string fileName, AndroidNet.Uri baseUri)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        if (baseUri == null)
        {
            throw new ArgumentNullException(nameof(baseUri));
        }

        var contentResolver = Platform.CurrentActivity.ContentResolver;

        try
        {
            var projection = new[]
            {
                MediaStore.Images.Media.InterfaceConsts.Id,
                MediaStore.IMediaColumns.Title,
                MediaStore.Images.Media.InterfaceConsts.DisplayName,
                MediaStore.IMediaColumns.MimeType,
                MediaStore.Images.Media.InterfaceConsts.VolumeName,
                MediaStore.Images.Media.InterfaceConsts.DocumentId,
                MediaStore.Images.Media.InterfaceConsts.RelativePath
            };

            var selection = $"{MediaStore.Images.Media.InterfaceConsts.DisplayName} = '{fileName}'";
            var mediaCursor = contentResolver.Query(baseUri, projection, selection, null, null);

            var columnIndexIdIndex = mediaCursor.GetColumnIndexOrThrow(MediaStore.Images.Media.InterfaceConsts.Id);
            var titleIndex = mediaCursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.Title);
            var displayNameIndex = mediaCursor.GetColumnIndexOrThrow(MediaStore.Images.Media.InterfaceConsts.DisplayName);
            var mimeTypeIndex = mediaCursor.GetColumnIndexOrThrow(MediaStore.IMediaColumns.MimeType);
            var volumeNameIndex = mediaCursor.GetColumnIndexOrThrow(MediaStore.Images.Media.InterfaceConsts.VolumeName);
            var documentIdIndex = mediaCursor.GetColumnIndexOrThrow(MediaStore.Images.Media.InterfaceConsts.DocumentId);
            var relativePathIndex = mediaCursor.GetColumnIndexOrThrow(MediaStore.Images.Media.InterfaceConsts.RelativePath);

            var items = new List<string>();

            while (mediaCursor.MoveToNext())
            {
                var id = mediaCursor.GetLong(columnIndexIdIndex);
                var title = mediaCursor.GetString(titleIndex);
                var displayName = mediaCursor.GetString(displayNameIndex);
                var mimeType = mediaCursor.GetString(mimeTypeIndex);
                var volumeName = mediaCursor.GetString(volumeNameIndex);
                var documentId = mediaCursor.GetString(documentIdIndex);
                var relativePath = mediaCursor.GetString(relativePathIndex);

                var contentUri = ContentUris.WithAppendedId(baseUri, id);

                return Task.FromResult(contentResolver.OpenInputStream(contentUri));
            }
        }
        catch (Exception e)
        {
            // TODO - Handle exception

            Console.WriteLine($"Error getting requested file \"{fileName}\" \"(uri: {baseUri})\"");
        }

        return null;
    }

    public async Task<string> WriteFileToInternalStorageAsync(string fileName, Stream stream)
    {
        var fullPath = fileName.Contains(FileSystem.CacheDirectory) ? fileName : Path.Combine(FileSystem.CacheDirectory, fileName);

        try
        {
            using var fileStream = File.Create(fullPath);

            stream.Seek(0, SeekOrigin.Begin);
            await stream.CopyToAsync(fileStream);

            fileStream.Close();
        }
        catch (Exception e)
        {
            // TODO - Handle exception

            Console.WriteLine($"Error writing requested file \"{fileName}\" to \"{fullPath}\"");

            return null;
        }

        return fullPath;
    }

    public async Task<string> WriteFileToInternalStorageAsync(string fileName, byte[] bytes)
    {
        using (var stream = new MemoryStream())
        {
            await stream.WriteAsync(bytes);

            var uri = await WriteFileToInternalStorageAsync(fileName, stream);

            return uri;
        }
    }

    public async Task<string> WriteImageFileToExternalStorageAsync(string fileName, Stream stream, bool isMask = false)
    {
        await checkForWritePermission();

        var contentValues = new ContentValues();
        contentValues.Put(MediaStore.IMediaColumns.Title, fileName);
        contentValues.Put(MediaStore.IMediaColumns.MimeType, "image/png");
        contentValues.Put(MediaStore.Images.Media.InterfaceConsts.DisplayName, fileName);
        contentValues.Put(MediaStore.Images.Media.InterfaceConsts.RelativePath, isMask ? extFolderNameMasks : extFolderName);

#if ANDROID30_0_OR_GREATER
        contentValues.Put(MediaStore.Images.Media.InterfaceConsts.IsPending, 1);
#endif

        AndroidNet.Uri uri;
        var contentResolver = Platform.CurrentActivity.ContentResolver;

        try
        {
            uri = contentResolver.Insert(MediaStore.Images.Media.ExternalContentUri, contentValues);

            var outputStream = contentResolver.OpenOutputStream(uri);
            await stream.CopyToAsync(outputStream);

#if ANDROID30_0_OR_GREATER
            contentValues.Clear();
            contentValues.Put(MediaStore.Images.Media.InterfaceConsts.IsPending, 0);
            contentResolver.Update(uri, contentValues, null);
#endif

            return uri.ToString();
        }
        catch (Exception e)
        {
            // TODO - Handle exception

            Console.WriteLine($"Error writing requested file \"{fileName}\" to \"{MediaStore.Images.Media.ExternalContentUri}\"");
        }

        return string.Empty;
    }

    public async Task<Mask> GetMaskFileFromAppDataAsync(string imageFileName)
    {
        await checkForWritePermission();

        var fileNameNoExtension = Path.GetFileNameWithoutExtension(imageFileName);
        var maskFileName = $"{fileNameNoExtension}.mask";
        var fullPath = Path.Combine(FileSystem.Current.AppDataDirectory, maskFileName);

        try
        {
            if (!File.Exists(fullPath))
            {
                return null;
            }

            using var reader = File.OpenText(fullPath);
            var contents = await reader.ReadToEndAsync();

            var options = new JsonSerializerOptions();
            options.Converters.Add(new ColorJsonConverter());

            var mask = JsonSerializer.Deserialize<Mask>(contents, options);

            return mask;

        }
        catch (Exception e)
        {
            Console.WriteLine($"Unable to read requested file \"{maskFileName}\" from \"{fullPath}\"");
        }

        return null;
    }

    public async Task<string> WriteMaskFileToAppDataAsync(string imageFileName, Mask mask)
    {
        await checkForWritePermission();

        var fileNameNoExtension = Path.GetFileNameWithoutExtension(imageFileName);
        var maskFileName = $"{fileNameNoExtension}.mask";
        var fullPath = Path.Combine(FileSystem.Current.AppDataDirectory, maskFileName);

        try
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ColorJsonConverter());

            var maskJson = JsonSerializer.Serialize(mask, options);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            using var fileStream = File.OpenWrite(fullPath);
            using var writer = new StreamWriter(fileStream);

            await writer.WriteAsync(maskJson);

            return fullPath;
        }
        catch (Exception e)
        {
            // TODO - Handle exception

            Console.WriteLine($"Error writing requested file \"{maskFileName}\" to \"{fullPath}\"");
        }

        return string.Empty;
    }

    private async Task checkForReadPermission()
    {
        if (await Permissions.CheckStatusAsync<Permissions.StorageRead>() != PermissionStatus.Granted)
        {
            var status = await Permissions.RequestAsync<Permissions.StorageRead>();

            if (status != PermissionStatus.Granted)
            {
                throw new PermissionException("The read storage permission has not been granted");
            }
        }
    }

    private async Task checkForWritePermission()
    {
#if ANDROID29_0_OR_GREATER
        // No need for permissions.
        await Task.CompletedTask;
#else
        if (await Permissions.CheckStatusAsync<Permissions.StorageWrite>() != PermissionStatus.Granted)
        {
            var status = await Permissions.RequestAsync<Permissions.StorageWrite>();
            
            if (status != PermissionStatus.Granted)
            {
                throw new PermissionException("The write storage permission has not been granted");
            }
        }
#endif
    }

    public Task<string[]> GetFileListFromInternalStorageAsync(string path = null)
    {
        try
        {
            var fullPath = path != null ? Path.Combine(FileSystem.CacheDirectory, path) : FileSystem.CacheDirectory;

            return Task.FromResult(Directory.GetFiles(fullPath));
        }
        catch
        {
            return null;
        }
    }

    public Task<bool> FileExistsInInternalStorageAsync(string filePath)
    {
        var fullPath = filePath.Contains(FileSystem.CacheDirectory) ? filePath : Path.Combine(FileSystem.CacheDirectory, filePath);

        return Task.FromResult(File.Exists(fullPath));
    }
}