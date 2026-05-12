using CommunityToolkit.Mvvm.Input;
using Mogri.Enums;
using SkiaSharp;

namespace Mogri.ViewModels;

/// <summary>
/// Canvas page view model partial that manages source-image changes, bitmap decoding, media replacement, palette updates, and persisted overlay restore.
/// </summary>
public partial class CanvasPageViewModel
{
    partial void OnSourceBitmapChanged(SKBitmap? value)
    {
        if (value == null)
        {
            return;
        }

        // Capture for lambda safety
        var bitmap = value;

        _ = Task.WhenAll(
            Task.Run(() => updateColorPalette(bitmap)),
            Task.Run(() => updateSegmentationImageAsync(bitmap)));
    }

    [RelayCommand]
    private async Task ShowMediaPicker()
    {
        try
        {
            var photo = await _popupService.PickSinglePhotoAsync();

            if (photo == null)
            {
                return;
            }

            const string newCanvasWithImageOption = "New Canvas with Image";
            const string scaleImageToExistingCanvasOption = "Scale Image to Existing Canvas";
            const string scaleImageToBoundingBoxOption = "Scale Image to Bounding Box";

            var actions = new List<string> { newCanvasWithImageOption, scaleImageToExistingCanvasOption };

            if (CurrentTool?.Type == ToolType.BoundingBox)
            {
                actions.Add(scaleImageToBoundingBoxOption);
            }

            var action = await _popupService.DisplayActionSheetAsync("Set Image", "Cancel", null, actions.ToArray());

            if (action == "Cancel" || action == null)
            {
                return;
            }

            using var fileStream = (await _fileService.OpenNormalizedPhotoStreamAsync(photo)).Stream;

            if (fileStream == null)
            {
                await _popupService.DisplayAlertAsync("Error", "Could not load the selected image.", "OK");
                return;
            }

            if (action == newCanvasWithImageOption)
            {
                await _canvasHistoryService.ClearAllAsync();
                ClearSegmentationMask();
                await LoadSourceBitmapUsingStream(fileStream, photo.FileName);
            }
            else if (action == scaleImageToExistingCanvasOption)
            {
                try
                {
                    IsBusy = true;

                    var loadedBitmap = LoadBitmapFromStream(fileStream);

                    if (loadedBitmap != null && SourceBitmap != null)
                    {
                        var info = new SKImageInfo(SourceBitmap.Width, SourceBitmap.Height);
                        var resizedBitmap = loadedBitmap.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));

                        loadedBitmap.Dispose();

                        SourceBitmap = resizedBitmap;
                        _sourceFileName = null;

                        ClearSegmentationMask();
                        await clearAllActionsAndHistoryAsync();
                    }
                }
                finally
                {
                    IsBusy = false;
                }
            }
            else if (action == scaleImageToBoundingBoxOption)
            {
                try
                {
                    IsBusy = true;

                    var snapshotId = await pushSnapshotAsync("Insert Image", false);

                    var loadedBitmap = LoadBitmapFromStream(fileStream);

                    if (loadedBitmap != null)
                    {
                        var stitchedBitmap = _canvasBitmapService.StitchBitmapIntoSource(SourceBitmap, loadedBitmap, BoundingBox, BoundingBoxScale);

                        loadedBitmap.Dispose();

                        if (snapshotId != null)
                        {
                            insertSnapshotMarker(snapshotId, "Insert Image", false);
                        }

                        SourceBitmap = stitchedBitmap;
                    }
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
        catch (Exception)
        {
            await _toastService.ShowAsync("Unable to load image. Please check permissions and try again.");
        }
    }

    private void updateColorPalette(SKBitmap bitmap)
    {
        GettingColorPalette = true;

        var palette = _imageService.ExtractColorPalette(bitmap, 48);
        if (palette != null)
        {
            _colorPalette = palette;
        }

        GettingColorPalette = false;
    }

    private SKBitmap? LoadBitmapFromStream(Stream? stream)
    {
        if (stream == null) return null;

        var codec = SKCodec.Create(stream);
        if (codec == null) return null;

        var info = new SKImageInfo
        {
            AlphaType = SKAlphaType.Unpremul,
            ColorSpace = codec.Info.ColorSpace,
            ColorType = codec.Info.ColorType,
            Height = codec.Info.Height,
            Width = codec.Info.Width,
        };

        return SKBitmap.Decode(codec, info);
    }

    private async Task LoadSourceBitmapUsingStream(Stream? stream, string fileName)
    {
        try
        {
            IsBusy = true;

            // Instead of a simple SKBitmap.Decode() call, we're using a codec and SKImageInfo with Unpremul for the
            // AlphaType so masked images can be reopened after being created
            var sourceBitmap = LoadBitmapFromStream(stream);

            // Wrap in dispatch call because ApplyQueryAttributes can call this method and it
            // appears to be called from a non-UI thread.
            await _mainThreadService.InvokeOnMainThreadAsync(() =>
            {
                SourceBitmap = sourceBitmap;
            });

            _sourceFileName = fileName;
            await clearAllActionsAndHistoryAsync();

            var mask = await _fileService.GetMaskFileFromAppDataAsync(_sourceFileName);

            void restorePersistedCanvasState()
            {
                if (mask == null)
                {
                    return;
                }

                var allActions = new List<CanvasActionViewModel>();

                if (mask.Lines != null)
                {
                    allActions.AddRange(mask.Lines);
                }

                if (mask.SegmentationMasks != null)
                {
                    allActions.AddRange(mask.SegmentationMasks);
                }

                restoreCanvasActions(allActions);
                restoreTextElements(mask.TextElements);
            }

            await _mainThreadService.InvokeOnMainThreadAsync(restorePersistedCanvasState);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await _popupService.DisplayAlertAsync("Error", "Failed to load mask data", "OK");
        }
        finally
        {
            if (stream != null)
            {
                await stream.DisposeAsync();
            }

            IsBusy = false;
        }
    }
}