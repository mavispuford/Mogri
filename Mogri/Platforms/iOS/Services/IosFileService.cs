using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using Mogri.Json;
using Mogri.Helpers;
using Mogri.Interfaces.Services;
using Mogri.ViewModels;
using Photos;
using UIKit;

namespace Mogri.Platforms.iOS.Services
{
    /// <summary>
    /// iOS-specific file service handling photo library access, mask persistence, and file I/O.
    /// </summary>
    public class IosFileService : IFileService
    {
        private readonly ILogger<IosFileService> _logger;
        private readonly IPopupService _popupService;

        public IosFileService(ILogger<IosFileService> logger, IPopupService popupService)
        {
            _logger = logger;
            _popupService = popupService;
        }

        public Task<bool> DeleteFileFromInternalStorageAsync(string filePath)
        {
            try
            {
                var safePath = Path.Combine(FileSystem.CacheDirectory, Path.GetFileName(filePath));
                if (File.Exists(safePath))
                {
                    File.Delete(safePath);
                    return Task.FromResult(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file from internal storage");
            }
            return Task.FromResult(false);
        }

        public Task<bool> FileExistsInInternalStorageAsync(string filePath)
        {
            var safePath = Path.Combine(FileSystem.CacheDirectory, Path.GetFileName(filePath));
            return Task.FromResult(File.Exists(safePath));
        }

        public Task<Stream?> GetFileStreamUsingExactUriAsync(string uriString)
        {
            try
            {
                if (File.Exists(uriString))
                {
                    return Task.FromResult<Stream?>(File.OpenRead(uriString));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file stream using exact URI");
            }
            return Task.FromResult<Stream?>(null);
        }

        public Task<Stream?> GetFileStreamFromExternalStorageAsync(string fileName)
        {
            // iOS generally handles files within app sandbox unless picking from typical document pickers.
            return Task.FromResult<Stream?>(null);
        }

        public Task<Stream?> GetFileStreamFromInternalStorageAsync(string fileName)
        {
            try
            {
                var filePath = Path.Combine(FileSystem.CacheDirectory, Path.GetFileName(fileName));
                if (File.Exists(filePath))
                {
                    return Task.FromResult<Stream?>(File.OpenRead(filePath));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file stream from internal storage");
            }
            return Task.FromResult<Stream?>(null);
        }

        public Task<string[]> GetFileListFromInternalStorageAsync(string? path = null)
        {
            try
            {
                var targetPath = path ?? FileSystem.CacheDirectory;
                if (Directory.Exists(targetPath))
                {
                    return Task.FromResult(Directory.GetFiles(targetPath));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file list from internal storage");
            }
            return Task.FromResult(Array.Empty<string>());
        }

        public async Task<MaskViewModel?> GetMaskFileFromAppDataAsync(string imageFileName)
        {
            var maskFileName = Path.ChangeExtension(Path.GetFileName(imageFileName), ".json");
            var maskFilePath = Path.Combine(FileSystem.AppDataDirectory, maskFileName);

            if (File.Exists(maskFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(maskFilePath);
                    return JsonSerializer.Deserialize<MaskViewModel>(json, MogriJsonSerializer.Options);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading mask file from AppData");

                    // Remove corrupt mask file so it doesn't block future reads
                    try { File.Delete(maskFilePath); } catch { /* best effort */ }
                }
            }
            return null;
        }

        public async Task<string> WriteFileToInternalStorageAsync(string fileName, Stream stream)
        {
            var filePath = Path.Combine(FileSystem.CacheDirectory, Path.GetFileName(fileName));
            try
            {
                using var fileStream = File.Create(filePath);
                if (stream.Position != 0 && stream.CanSeek)
                {
                    stream.Position = 0;
                }
                await stream.CopyToAsync(fileStream);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing stream to internal storage");
                return string.Empty;
            }
        }

        public async Task<string> WriteFileToInternalStorageAsync(string fileName, byte[] bytes)
        {
            var filePath = Path.Combine(FileSystem.CacheDirectory, Path.GetFileName(fileName));
            try
            {
                await File.WriteAllBytesAsync(filePath, bytes);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing bytes to internal storage");
                return string.Empty;
            }
        }

        public Task<bool> DeleteMaskFileFromAppDataAsync(string imageFileName)
        {
            var maskFileName = Path.ChangeExtension(Path.GetFileName(imageFileName), ".json");
            var maskFilePath = Path.Combine(FileSystem.AppDataDirectory, maskFileName);

            try
            {
                if (File.Exists(maskFilePath))
                {
                    File.Delete(maskFilePath);
                    return Task.FromResult(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to delete mask file from AppData");
            }

            return Task.FromResult(false);
        }

        public async Task<string> WriteMaskFileToAppDataAsync(string imageFileName, MaskViewModel mask)
        {
            var maskFileName = Path.ChangeExtension(Path.GetFileName(imageFileName), ".json");
            var maskFilePath = Path.Combine(FileSystem.AppDataDirectory, maskFileName);

            try
            {
                var json = JsonSerializer.Serialize(mask, MogriJsonSerializer.Options);

                // Write to a temp file first, then atomically move into place to
                // prevent corruption if the process is killed mid-write.
                var tempPath = maskFilePath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json);
                File.Move(tempPath, maskFilePath, overwrite: true);

                return maskFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing mask file to AppData");
                return string.Empty;
            }
        }

        public async Task<string> WriteImageFileToExternalStorageAsync(string fileName, Stream stream, bool isMask = false)
        {
            try
            {
                var authorizationStatus = PHPhotoLibrary.GetAuthorizationStatus(PHAccessLevel.ReadWrite);
                
                if (authorizationStatus != PHAuthorizationStatus.Authorized)
                {
                    authorizationStatus = await PHPhotoLibrary.RequestAuthorizationAsync(PHAccessLevel.ReadWrite);
                }

                if (authorizationStatus == PHAuthorizationStatus.Authorized)
                {
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var imageData = NSData.FromArray(memoryStream.ToArray());
                    var uiImage = UIImage.LoadFromData(imageData);

                    if (uiImage != null)
                    {
                        var tcs = new TaskCompletionSource<string>();

                        PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(() =>
                        {
                            PHAssetChangeRequest.FromImage(uiImage);
                        }, (success, error) =>
                        {
                            if (success)
                            {
                                tcs.SetResult(fileName); // iOS photo library doesn't easily expose the raw file path, returning filename conceptually.
                            }
                            else
                            {
                                _logger.LogError($"Failed to save image to photo library. Error: {error?.LocalizedDescription}");
                                tcs.SetResult(string.Empty);
                            }
                        });

                        return await tcs.Task;
                    }
                }
                else
                {
                    _logger.LogWarning("Photo library access denied.");
                    await _popupService.DisplayAlertAsync("Permission Denied", "Cannot save photo without access to the Photo Library.", "OK");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing image file to external storage on iOS");
            }
            
            return string.Empty;
        }

        public async Task<(Stream? Stream, string ContentType)> OpenNormalizedPhotoStreamAsync(FileResult photo)
        {
            var isHeic = isHeicFile(photo);

            if (!isHeic)
            {
                using var stream = await photo.OpenReadAsync();
                var (correctedStream, wasRotated) = ImageOrientationHelper.ApplyExifOrientation(stream);
                return (correctedStream, wasRotated ? "image/jpeg" : photo.ContentType);
            }

            try
            {
                // UIImage can decode HEIC natively on iOS; re-encode as JPEG for SkiaSharp compatibility.
                using var nsData = NSData.FromFile(photo.FullPath);

                if (nsData == null)
                {
                    return (null, photo.ContentType);
                }

                var image = UIImage.LoadFromData(nsData);

                if (image == null)
                {
                    return (null, photo.ContentType);
                }

                // UIImage.AsJPEG preserves the EXIF orientation tag but does not bake
                // it into pixel data. Draw into a new context to normalize orientation.
                if (image.Orientation != UIImageOrientation.Up)
                {
                    var normalized = normalizeImageOrientation(image);
                    image.Dispose();
                    image = normalized;
                }

                var jpegData = image.AsJPEG(1.0f);
                image.Dispose();

                if (jpegData == null)
                {
                    return (null, photo.ContentType);
                }

                var ms = new MemoryStream(jpegData.ToArray());
                jpegData.Dispose();
                return (ms, "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert HEIC to JPEG on iOS");
                return (null, photo.ContentType);
            }
        }

        /// <summary>
        /// Draws the UIImage into a new graphics context, baking EXIF orientation
        /// into the pixel data so the resulting image has UIImageOrientation.Up.
        /// </summary>
        private static UIImage normalizeImageOrientation(UIImage image)
        {
            var renderer = new UIGraphicsImageRenderer(image.Size);
            return renderer.CreateImage(context =>
            {
                image.Draw(new CGRect(0, 0, image.Size.Width, image.Size.Height));
            });
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
    }
}
