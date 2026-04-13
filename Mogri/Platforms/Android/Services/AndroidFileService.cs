using Android.Content;
using Android.Provider;
using Microsoft.Extensions.Logging;
using Mogri.Interfaces.Services;
using Mogri.Models;
using Mogri.ViewModels;
using System.Text.Json;
using AndroidNet = Android.Net;
using AndroidBitmap = global::Android.Graphics.Bitmap;
using AndroidBitmapFactory = global::Android.Graphics.BitmapFactory;

namespace Mogri.Platforms.Android.Services;

/// <summary>
/// Android-specific file service handling media storage, mask persistence, and permissions.
/// </summary>
public class AndroidFileService : IFileService
{
    private const string ExtFolderName = "Pictures/Mogri/";

    private const string ExtFolderNameMasks = "Pictures/Mogri/Masks/";

    private readonly ILogger<AndroidFileService> _logger;
    private readonly IPopupService _popupService;

    public AndroidFileService(ILogger<AndroidFileService> logger, IPopupService popupService)
    {
        _logger = logger;
        _popupService = popupService;
    }

    public async Task<Stream?> GetFileStreamUsingExactUriAsync(string uriString)
    {
        await checkForReadPermission();

        var contentResolver = Platform.CurrentActivity?.ContentResolver;

        if (contentResolver == null) return null;

        try
        {
            var uri = AndroidNet.Uri.Parse(uriString);
            if (uri == null) return null;

            return contentResolver.OpenInputStream(uri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting requested file (uri: {uriString})");
        }

        return null;
    }

    public async Task<Stream?> GetFileStreamFromExternalStorageAsync(string fileName)
    {
        await checkForReadPermission();

        if (MediaStore.Images.Media.ExternalContentUri == null) return null;

        return await getFileStreamFromStorageUsingBaseUri(fileName, MediaStore.Images.Media.ExternalContentUri);

        //return await getFileStreamFromStorageUsingBaseUri(fileName, AndroidNet.Uri.WithAppendedPath(MediaStore.Images.Media.ExternalContentUri, ExtFolderName));
    }

    public Task<Stream?> GetFileStreamFromInternalStorageAsync(string fileName)
    {
        var fullPath = fileName.Contains(FileSystem.CacheDirectory) ? fileName : Path.Combine(FileSystem.CacheDirectory, fileName);

        try
        {
            var reader = new StreamReader(fullPath, true);

            return Task.FromResult<Stream?>(reader.BaseStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading requested file from {fullPath}");
        }

        return Task.FromResult<Stream?>(null);
    }

    private Task<Stream?> getFileStreamFromStorageUsingBaseUri(string fileName, AndroidNet.Uri baseUri)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        if (baseUri == null)
        {
            throw new ArgumentNullException(nameof(baseUri));
        }

        var contentResolver = Platform.CurrentActivity?.ContentResolver;

        if (contentResolver == null) return Task.FromResult<Stream?>(null);

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

            if (mediaCursor == null) return Task.FromResult<Stream?>(null);

            var columnIndexIdIndex = mediaCursor.GetColumnIndexOrThrow(MediaStore.Images.Media.InterfaceConsts.Id);

            while (mediaCursor.MoveToNext())
            {
                var id = mediaCursor.GetLong(columnIndexIdIndex);
                var contentUri = ContentUris.WithAppendedId(baseUri, id);
                if (contentUri == null) continue;

                return Task.FromResult(contentResolver.OpenInputStream(contentUri));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting requested file {fileName} (uri: {baseUri})");
        }

        return Task.FromResult<Stream?>(null);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error writing requested file {fileName} to {fullPath}");

            return string.Empty;
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
        contentValues.Put(MediaStore.Images.Media.InterfaceConsts.RelativePath, isMask ? ExtFolderNameMasks : ExtFolderName);

#if ANDROID30_0_OR_GREATER
        contentValues.Put(MediaStore.Images.Media.InterfaceConsts.IsPending, 1);
#endif

        AndroidNet.Uri? uri;
        var contentResolver = Platform.CurrentActivity?.ContentResolver;

        if (contentResolver == null) return string.Empty;

        if (MediaStore.Images.Media.ExternalContentUri == null) return string.Empty;

        try
        {
            uri = contentResolver.Insert(MediaStore.Images.Media.ExternalContentUri, contentValues);

            if (uri != null)
            {
                var outputStream = contentResolver.OpenOutputStream(uri);
                if (outputStream != null)
                {
                    await stream.CopyToAsync(outputStream);
                }
            }
#if ANDROID30_0_OR_GREATER
            contentValues.Clear();
            contentValues.Put(MediaStore.Images.Media.InterfaceConsts.IsPending, 0);
            if (uri != null)
            {
                contentResolver.Update(uri, contentValues, null, null);
            }
#endif

            return uri?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error writing requested file {fileName} to {MediaStore.Images.Media.ExternalContentUri}");
            await _popupService.DisplayAlertAsync("Error", "Failed to save image to gallery.", "OK");
        }

        return string.Empty;
    }

    public async Task<MaskViewModel?> GetMaskFileFromAppDataAsync(string imageFileName)
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

            var mask = JsonSerializer.Deserialize<MaskViewModel>(contents, options);

            return mask;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unable to read requested file {maskFileName} from {fullPath}");

            // Remove corrupt mask file so it doesn't block future reads
            try { File.Delete(fullPath); } catch { /* best effort */ }
        }

        return null;
    }

    public Task<bool> DeleteMaskFileFromAppDataAsync(string imageFileName)
    {
        var fileNameNoExtension = Path.GetFileNameWithoutExtension(imageFileName);
        var maskFileName = $"{fileNameNoExtension}.mask";
        var fullPath = Path.Combine(FileSystem.Current.AppDataDirectory, maskFileName);

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unable to delete mask file {maskFileName} from {fullPath}");
        }

        return Task.FromResult(false);
    }

    public async Task<string> WriteMaskFileToAppDataAsync(string imageFileName, MaskViewModel mask)
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

            // Write to a temp file first, then atomically move into place to
            // prevent corruption if the process is killed mid-write.
            var tempPath = fullPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, maskJson);
            File.Move(tempPath, fullPath, overwrite: true);

            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error writing requested file {maskFileName} to {fullPath}");
        }

        return string.Empty;
    }

    public async Task<(Stream? Stream, string ContentType)> OpenNormalizedPhotoStreamAsync(FileResult photo)
    {
        var isHeic = isHeicFile(photo);

        if (!isHeic)
        {
            return (await photo.OpenReadAsync(), photo.ContentType);
        }

        try
        {
            // Android's BitmapFactory can decode HEIC; re-encode as JPEG for SkiaSharp compatibility.
            var bitmap = await AndroidBitmapFactory.DecodeFileAsync(photo.FullPath);

            if (bitmap == null)
            {
                return (null, photo.ContentType);
            }

            var ms = new MemoryStream();
            await bitmap.CompressAsync(AndroidBitmap.CompressFormat.Jpeg!, 100, ms);
            bitmap.Recycle();
            bitmap.Dispose();
            ms.Seek(0, SeekOrigin.Begin);
            return (ms, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert HEIC to JPEG on Android");
            return (null, photo.ContentType);
        }
    }

    private static bool isHeicFile(FileResult photo)
    {
        var contentType = photo.ContentType?.ToLowerInvariant();
        if (contentType is "image/heic" or "image/heif")
        {
            return true;
        }

        var ext = Path.GetExtension(photo.FileName)?.ToLowerInvariant();
        return ext is ".heic" or ".heif";
    }

    private async Task checkForReadPermission()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            if (await Permissions.CheckStatusAsync<Permissions.Photos>() != PermissionStatus.Granted)
            {
                var status = await Permissions.RequestAsync<Permissions.Photos>();

                if (status != PermissionStatus.Granted)
                {
                    throw new PermissionException("The photos permission has not been granted");
                }
            }
        }
        else
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

    public Task<string[]> GetFileListFromInternalStorageAsync(string? path = null)
    {
        try
        {
            var fullPath = path != null ? Path.Combine(FileSystem.CacheDirectory, path) : FileSystem.CacheDirectory;

            var info = new DirectoryInfo(fullPath);
            var files = info.GetFiles().OrderByDescending(p => p.CreationTime).Select(f => f.FullName).ToArray();

            return Task.FromResult(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file list from internal storage");
            return Task.FromResult(Array.Empty<string>());
        }
    }

    public Task<bool> FileExistsInInternalStorageAsync(string filePath)
    {
        var fullPath = filePath.Contains(FileSystem.CacheDirectory) ? filePath : Path.Combine(FileSystem.CacheDirectory, filePath);

        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<bool> DeleteFileFromInternalStorageAsync(string filePath)
    {
        var fullPath = filePath.Contains(FileSystem.CacheDirectory) ? filePath : Path.Combine(FileSystem.CacheDirectory, filePath);

        try
        {
            File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting file from internal storage (path: {fullPath})");
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}
