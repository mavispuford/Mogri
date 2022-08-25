using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.OS;
using Microsoft.Maui.ApplicationModel;
using Android.Content;
using MobileDiffusion.Interfaces.Services;

namespace MobileDiffusion.Services;

public class FileService : IFileService
{
    public Stream GetFileStreamFromExternalStorage(string fileName)
    {
        return default(Stream);
    }

    public async Task WriteFileToExternalStorageAsync(string fileName, Stream stream)
    {
        var contentValues = new ContentValues();
        contentValues.Put(Android.Provider.MediaStore.IMediaColumns.Title, fileName);
        contentValues.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, "image/jpg");
        contentValues.Put(Android.Provider.MediaStore.Images.Media.InterfaceConsts.DisplayName, "mask");
        contentValues.Put(Android.Provider.MediaStore.Images.Media.InterfaceConsts.IsPending, 1);

        Android.Net.Uri uri;
        var contentResolver = Platform.CurrentActivity.ContentResolver;

        try
        {
            uri = contentResolver.Insert(Android.Provider.MediaStore.Downloads.ExternalContentUri, contentValues);

            var outputStream = contentResolver.OpenOutputStream(uri);
            await stream.CopyToAsync(outputStream);

            contentValues.Clear();
            contentValues.Put(Android.Provider.MediaStore.Images.Media.InterfaceConsts.IsPending, 0);
            contentResolver.Update(uri, contentValues, null);
        }
        catch (Exception e)
        {
            // TODO - Handle exception
        }
    }
}