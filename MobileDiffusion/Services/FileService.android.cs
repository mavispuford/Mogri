using Android.Content;
using Android.Provider;
using MobileDiffusion.Interfaces.Services;

namespace MobileDiffusion.Services;

public class FileService : IFileService
{
    public async Task<Stream> GetFileStreamFromExternalStorage(string fileName, string uriString)
    {
        if (await Permissions.CheckStatusAsync<Permissions.StorageRead>() != PermissionStatus.Granted)
        {
            var status = await Permissions.RequestAsync<Permissions.StorageRead> ();

            if (status != PermissionStatus.Granted)
            {
                throw new PermissionException("The read storage permission has not been granted");
            }
        }

        var contentResolver = Platform.CurrentActivity.ContentResolver;

        try
        {
            var uri = string.IsNullOrEmpty(uriString) ? MediaStore.Images.Media.ExternalContentUri :
                Android.Net.Uri.Parse(uriString);

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

            var selection = string.IsNullOrEmpty(uriString) ? $"{MediaStore.Images.Media.InterfaceConsts.DisplayName} = '{fileName}'" : null;
            //var selection = $"{MediaStore.Images.Media.InterfaceConsts.Title} = '{Path.GetFileNameWithoutExtension(fileName)}'";
            var mediaCursor = contentResolver.Query(uri, projection, selection, null, null);
           
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

                var contentUri = ContentUris.WithAppendedId(MediaStore.Images.Media.ExternalContentUri, id);

                return contentResolver.OpenInputStream(contentUri);
            }
        }
        catch (Exception e)
        {
            // TODO - Handle exception

            Console.WriteLine($"Error getting requested file \"{fileName}\" \"(uri: {uriString})\"");
        }

        return null;
    }

    public async Task<string> WriteFileToExternalStorageAsync(string fileName, Stream stream)
    {
#if ANDROID29_0_OR_GREATER
        // No need for permissions.
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

            Console.WriteLine($"Error writing requested file \"{fileName}\"");
        }

        return string.Empty;
    }
}