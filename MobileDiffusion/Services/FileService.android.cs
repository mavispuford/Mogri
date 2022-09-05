using Android.Content;
using Android.Provider;
using Android.Service.QuickSettings;
using MobileDiffusion.Interfaces.Services;

namespace MobileDiffusion.Services;

public class FileService : IFileService
{
    private const string extFolderName = "MobileDiffusion/";

    public async Task<Stream> GetFileStreamUsingExactUriAsync(string uriString)
    {
        await checkForReadPermission();

        var contentResolver = Platform.CurrentActivity.ContentResolver;

        try
        {
            var uri = Android.Net.Uri.Parse(uriString);

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

        //return await getFileStreamFromStorageUsingBaseUri(fileName, Android.Net.Uri.WithAppendedPath(MediaStore.Images.Media.ExternalContentUri, extFolderName));
    }

    public Task<Stream> GetFileStreamFromInternalStorageAsync(string fileName)
    {
        var fullPath = Path.Combine(FileSystem.CacheDirectory, fileName);

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

    private Task<Stream> getFileStreamFromStorageUsingBaseUri(string fileName, Android.Net.Uri baseUri)
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
        var fullPath = Path.Combine(FileSystem.CacheDirectory, fileName);

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

    public async Task<string> WriteFileToExternalStorageAsync(string fileName, Stream stream)
    {
        await checkForWritePermission();

        //return await writeFileToBaseUriAsync(fileName, stream, Android.Net.Uri.WithAppendedPath(MediaStore.Images.Media.ExternalContentUri, extFolderName));
        return await writeFileToBaseUriAsync(fileName, stream, MediaStore.Images.Media.ExternalContentUri);
    }

    private async Task<string> writeFileToBaseUriAsync(string fileName, Stream stream, Android.Net.Uri baseUri)
    {
        var contentValues = new ContentValues();
        contentValues.Put(MediaStore.IMediaColumns.Title, fileName);
        contentValues.Put(MediaStore.IMediaColumns.MimeType, "image/jpg");
        contentValues.Put(MediaStore.Images.Media.InterfaceConsts.DisplayName, fileName);

#if ANDROID30_0_OR_GREATER
        contentValues.Put(MediaStore.Images.Media.InterfaceConsts.IsPending, 1);
#endif

        Android.Net.Uri uri;
        var contentResolver = Platform.CurrentActivity.ContentResolver;

        try
        {
            uri = contentResolver.Insert(baseUri, contentValues);

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

            Console.WriteLine($"Error writing requested file \"{fileName}\" to \"{baseUri}\"");
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
}