using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.Input;
using Mogri.Enums;
using Mogri.Models;
using SkiaSharp;

namespace Mogri.ViewModels;

public partial class CanvasPageViewModel
{
    partial void OnSourceBitmapChanged(SKBitmap? value)
    {
        if (value != null)
        {
            // Capture for lambda safety
            var bitmap = value;

            _ = Task.WhenAll(Task.Run(() =>
            {
                GettingColorPalette = true;

                var palette = _imageService.ExtractColorPalette(bitmap, 48);
                if (palette != null)
                {
                    _colorPalette = palette;
                }

                GettingColorPalette = false;
            }), Task.Run(async () =>
            {
                // Latest-wins guard for SourceBitmap changes. A canceled older request can still finish later,
                // so only the newest version is allowed to publish its result.

                var magicWandTool = AvailableTools.FirstOrDefault(t => t.Type == ToolType.MagicWand);
                CancellationTokenSource? previousSetSegmentationImageCancellationTokenSource;
                CancellationTokenSource currentSetSegmentationImageCancellationTokenSource;
                int currentSetSegmentationImageVersion;

                lock (_setSegmentationImageLock)
                {
                    _setSegmentationImageRequestCount++;
                    _setSegmentationImageVersion++;
                    currentSetSegmentationImageVersion = _setSegmentationImageVersion;

                    // Swap in a new token source under the lock so the previous request can be canceled
                    // after the new request is fully registered as the current one.
                    previousSetSegmentationImageCancellationTokenSource = _setSegmentationImageCancellationTokenSource;
                    currentSetSegmentationImageCancellationTokenSource = new CancellationTokenSource();
                    _setSegmentationImageCancellationTokenSource = currentSetSegmentationImageCancellationTokenSource;

                    HasSegmentationImage = false;

                    if (magicWandTool != null)
                    {
                        magicWandTool.IsLoading = true;
                    }

                    SettingSegmentationImage = true;
                }

                if (previousSetSegmentationImageCancellationTokenSource != null)
                {
                    if (!previousSetSegmentationImageCancellationTokenSource.IsCancellationRequested)
                    {
                        previousSetSegmentationImageCancellationTokenSource.Cancel();
                    }

                    previousSetSegmentationImageCancellationTokenSource.Dispose();
                }

                var hasSegmentationImage = false;

                try
                {
                    hasSegmentationImage = await _segmentationService.SetImage(bitmap, currentSetSegmentationImageCancellationTokenSource.Token);
                }
                finally
                {
                    var shouldDisposeCurrentSetSegmentationImageCancellationTokenSource = false;

                    lock (_setSegmentationImageLock)
                    {
                        // A stale request may still complete after cancellation, but it must not change the
                        // UI flags if a newer SourceBitmap has already started processing.
                        if (currentSetSegmentationImageVersion == _setSegmentationImageVersion)
                        {
                            HasSegmentationImage = hasSegmentationImage;
                        }

                        _setSegmentationImageRequestCount--;

                        SettingSegmentationImage = _setSegmentationImageRequestCount > 0;

                        if (magicWandTool != null)
                        {
                            magicWandTool.IsLoading = _setSegmentationImageRequestCount > 0;
                        }

                        if (ReferenceEquals(_setSegmentationImageCancellationTokenSource, currentSetSegmentationImageCancellationTokenSource))
                        {
                            _setSegmentationImageCancellationTokenSource = null;
                            shouldDisposeCurrentSetSegmentationImageCancellationTokenSource = true;
                        }
                    }

                    if (shouldDisposeCurrentSetSegmentationImageCancellationTokenSource)
                    {
                        currentSetSegmentationImageCancellationTokenSource.Dispose();
                    }
                }

            }));
        }
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
            await Toast.Make("Unable to load image. Please check permissions and try again.").Show();
        }
    }

    [RelayCommand]
    private async Task DoSegmentation(SKPoint[] points)
    {
        if (_doingSegmentation ||
            points == null ||
            points.Length == 0)
        {
            return;
        }

        if (SourceBitmap == null)
        {
            await _popupService.DisplayAlertAsync("No image", "There is no image on the canvas. Add an image and try again.", "OK");

            return;
        }

        if (!HasSegmentationImage)
        {
            if (SettingSegmentationImage)
            {
                await _popupService.DisplayAlertAsync("Processing...", "The current image is still processing. Please try again.", "OK");
            }
            else
            {
                await _popupService.DisplayAlertAsync("Problem", "There was a problem processing the current image. Please add an image and try again.", "OK");
            }

            return;
        }

        try
        {
            _doingSegmentation = true;
            IsBusy = true;

            var maskBitmap = await _segmentationService.DoSegmentation(points);

            if (maskBitmap != null)
            {
                if (SegmentationBitmap == null)
                {
                    SegmentationBitmap = maskBitmap;
                }
                else
                {
                    var newBitmap = new SKBitmap(SegmentationBitmap.Info);

                    using (var combineCanvas = new SKCanvas(newBitmap))
                    {
                        var paint = new SKPaint
                        {
                            BlendMode = SKBlendMode.SrcOver
                        };

                        combineCanvas.DrawBitmap(SegmentationBitmap, 0, 0, paint);

                        paint.BlendMode = SegmentationAdd ? SKBlendMode.SrcOver : SKBlendMode.DstOut;

                        combineCanvas.DrawBitmap(maskBitmap, 0, 0, paint);
                    }

                    SegmentationBitmap?.Dispose();
                    SegmentationBitmap = null;

                    SegmentationBitmap = newBitmap;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await Toast.Make("Failed to perform segmentation.").Show();
        }
        finally
        {
            IsBusy = false;
            _doingSegmentation = false;
        }
    }

    [RelayCommand]
    private async Task InvertSegmentationMask()
    {
        if (SegmentationBitmap == null)
        {
            if (SourceBitmap == null) return;

            // If no existing segmentation mask, the inverted state is simply a completely filled mask
            var fullMask = new SKBitmap(SourceBitmap.Info);
            using var canvas = new SKCanvas(fullMask);
            canvas.Clear(_segmentationService.MaskColor);

            SegmentationBitmap = fullMask;
            return;
        }

        try
        {
            IsBusy = true;

            var invertedBitmap = await Task.Run(() =>
            {
                return _segmentationService.InvertMask(SegmentationBitmap);
            });

            var oldBitmap = SegmentationBitmap;
            SegmentationBitmap = invertedBitmap;
            oldBitmap?.Dispose();

            // Reset SAM state so subsequent taps start fresh instead of building on the pre-inverted state
            _segmentationService.Reset();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await Toast.Make("Failed to invert mask.").Show();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplySegmentationMask()
    {
        if (SegmentationBitmap == null)
        {
            return;
        }

        try
        {
            IsBusy = true;

            var maskBitmap = await Task.Run(() =>
            {
                return _canvasBitmapService.CreateMaskBitmapFromSegmentationMask(SegmentationBitmap);
            });

            var segmentationMask = new SegmentationMaskViewModel
            {
                CanvasActionType = CanvasActionType.Mask,
                Order = getNextCanvasOrder(),
                Color = CurrentColor,
                Alpha = (float)CurrentAlpha,
                Noise = CurrentNoise,
                Bitmap = maskBitmap
            };

            CanvasActions.Add(segmentationMask);

            ClearSegmentationMask();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await Toast.Make("Failed to apply mask.").Show();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearSegmentationMask()
    {
        SegmentationBitmap?.Dispose();
        SegmentationBitmap = null;
        _segmentationService.Reset();
    }

    [RelayCommand]
    private void ToggleSegmentationAdd()
    {
        SegmentationAdd = !SegmentationAdd;

        _segmentationService.Reset();
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
            var dispatcher = Dispatcher.GetForCurrentThread();
            if (dispatcher != null)
            {
                await dispatcher.DispatchAsync(() =>
                {
                    SourceBitmap = sourceBitmap;
                });
            }
            else
            {
                SourceBitmap = sourceBitmap;
            }

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

            if (dispatcher != null)
            {
                await dispatcher.DispatchAsync(() =>
                {
                    restorePersistedCanvasState();
                });
            }
            else
            {
                restorePersistedCanvasState();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await _popupService.DisplayAlertAsync("Error", "Failed to load mask data", "OK");
        }
        finally
        {
            if (stream != null)
                await stream.DisposeAsync();

            IsBusy = false;
        }
    }
}