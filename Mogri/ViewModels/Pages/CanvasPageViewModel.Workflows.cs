using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.Input;
using Mogri.Enums;
using Mogri.Helpers;
using Mogri.Models;
using SkiaSharp;

namespace Mogri.ViewModels;

/// <summary>
/// Canvas page view model partial that orchestrates save, send, crop, flatten, patch, and workflow payload preparation.
/// </summary>
public partial class CanvasPageViewModel
{
    private const string PngContentType = "image/png";

    [RelayCommand]
    private async Task Save()
    {
        await saveImage();
    }

    private async Task saveImage()
    {
        if (SourceBitmap == null)
        {
            await Toast.Make("There is no image to save.").Show();

            return;
        }

        await (PrepareForSavingCommand?.ExecuteAsync(FinishSavingCommand) ?? Task.CompletedTask);
    }

    private async Task<bool> AskUseCanvasMode()
    {
        var hasMaskActions = CanvasActions != null
            && CanvasActions.Any(canvasAction => canvasAction.CanvasActionType == CanvasActionType.Mask);

        if (!hasMaskActions)
        {
            _currentCanvasUseMode = CanvasUseMode.ImageOnly;
            return true;
        }

        const string inpaint = "Paint and Mask (inpainting)";
        const string paintOnly = "Paint only (NO mask)";
        const string maskOnly = "Mask only (NO Paint)";
        const string imageOnly = "Image only";

        var selection = await _popupService.DisplayActionSheetAsync("Image Mode", "Cancel", null, inpaint, paintOnly, maskOnly, imageOnly);

        if (selection == inpaint) _currentCanvasUseMode = CanvasUseMode.Inpaint;
        else if (selection == paintOnly) _currentCanvasUseMode = CanvasUseMode.PaintOnly;
        else if (selection == maskOnly) _currentCanvasUseMode = CanvasUseMode.MaskOnly;
        else if (selection == imageOnly) _currentCanvasUseMode = CanvasUseMode.ImageOnly;
        else return false;

        return true;
    }

    [RelayCommand]
    private async Task SendToImageToImage()
    {
        ShowActions = false;

        if (SourceBitmap == null)
        {
            await Toast.Make("There is no image to send.").Show();

            return;
        }

        if (!await AskUseCanvasMode())
        {
            return;
        }

        await (PrepareForSavingCommand?.ExecuteAsync(FinishSendingToImageToImageCommand) ?? Task.CompletedTask);
    }

    [RelayCommand]
    private async Task BeginCropImageRect()
    {
        ShowActions = false;

        if (SourceBitmap == null)
        {
            await Toast.Make("There is no image data to crop.").Show();

            return;
        }

        if (!await AskUseCanvasMode())
        {
            return;
        }

        await (PrepareForSavingCommand?.ExecuteAsync(FinishCroppingWithBoundingBoxCommand) ?? Task.CompletedTask);
    }

    [RelayCommand]
    private async Task FinishSaving(CanvasCaptureResult result)
    {
        await ExecuteWithPreparedSourceBitmapAsync(result, async (sourceBitmap, _) =>
        {
            await Task.Run(async () =>
            {
                var dispatcher = Shell.Current.CurrentPage.Dispatcher;

                try
                {
                    using var memStream = new MemoryStream();
                    using var skiaStream = new SKManagedWStream(memStream);

                    sourceBitmap.Encode(skiaStream, SKEncodedImageFormat.Png, 100);

                    var fileName = $"CanvasImage-{DateTime.Now.Ticks}.png";
                    memStream.Seek(0, SeekOrigin.Begin);
                    await _fileService.WriteImageFileToExternalStorageAsync(fileName, memStream, false);

                    await (dispatcher?.DispatchAsync(async () =>
                    {
                        await Toast.Make($"{fileName} saved.").Show();
                    }) ?? Task.CompletedTask);
                }
                catch (Exception)
                {
                    await (dispatcher?.DispatchAsync(async () =>
                    {
                        await Toast.Make("Failed to save image. Please try again.").Show();
                    }) ?? Task.CompletedTask);
                }
            });

            return true;
        });
    }

    [RelayCommand]
    private async Task FinishSendingToImageToImage(CanvasCaptureResult result)
    {
        await ExecuteWithPreparedSourceBitmapAsync(result, async (sourceBitmap, _) =>
        {
            await Task.Run(async () =>
            {
                // Re-render the layer using the source bitmap dimensions so masks stay aligned 1:1.
                using var rawMaskBitmap = _canvasActionBitmapService.CreateRenderedLayer(CanvasActions, sourceBitmap.Width, sourceBitmap.Height);

                try
                {
                    var colorizedBitmap = CreatePreparedWorkflowBitmap(sourceBitmap, rawMaskBitmap);
                    using var ownedColorizedBitmap = colorizedBitmap != null && !ReferenceEquals(colorizedBitmap, sourceBitmap)
                        ? colorizedBitmap
                        : null;

                    var payload = ImagePayloadHelper.CreateConstrainedPayload(colorizedBitmap, PngContentType);
                    if (payload == null)
                    {
                        return;
                    }

                    var parameters = CreateWorkflowNavigationParameters(payload, CreateWorkflowMaskImageData(rawMaskBitmap));

                    var dispatcher = Shell.Current.CurrentPage?.Dispatcher;
                    await (dispatcher?.DispatchAsync(async () =>
                    {
                        await Shell.Current.GoToAsync("///MainPageTab", parameters);
                    }) ?? Task.CompletedTask);
                }
                catch
                {
                    // Ignored
                }
            });

            return true;
        });
    }

    [RelayCommand]
    private async Task ApplyPaintAndMasks()
    {
        ShowActions = false;

        if (SourceBitmap == null)
        {
            await Toast.Make("There is no image to apply paint/masks to.").Show();
            return;
        }

        var result = await _popupService.DisplayAlertAsync("Flatten Canvas?",
            "This will apply the paint/masks and replace the current canvas image. This can be undone from the Canvas History.\n\nContinue?",
            "YES",
            "NO");

        if (!result)
        {
            return;
        }

        await (PrepareForSavingCommand?.ExecuteAsync(FinishApplyingPaintAndMasksCommand) ?? Task.CompletedTask);
    }

    [RelayCommand]
    private async Task FinishApplyingPaintAndMasks(CanvasCaptureResult result)
    {
        await ExecuteWithPreparedSourceBitmapAsync(result, async (sourceBitmap, preparedSourceBitmap) =>
        {
            var snapshotId = await pushSnapshotAsync("Flatten", true);
            var transferredPreparedSourceBitmapOwnership = false;

            await Task.Run(async () =>
            {
                using var rawMaskBitmap = _canvasActionBitmapService.CreateRenderedLayer(CanvasActions, sourceBitmap.Width, sourceBitmap.Height);

                var hasMaskActions = CanvasActions.Any(canvasAction => canvasAction.CanvasActionType == CanvasActionType.Mask);
                var mergedBitmap = hasMaskActions
                    ? _canvasBitmapService.CreateMaskedBitmap(sourceBitmap, rawMaskBitmap)
                    : preparedSourceBitmap;

                if (mergedBitmap != null)
                {
                    var dispatcher = Shell.Current.CurrentPage?.Dispatcher;
                    await (dispatcher?.DispatchAsync(async () =>
                    {
                        var oldBitmap = SourceBitmap;
                        SourceBitmap = mergedBitmap;
                        oldBitmap?.Dispose();

                        CanvasActions.Clear();
                        TextElements.Clear();
                        ClearSegmentationMask();

                        if (ReferenceEquals(mergedBitmap, preparedSourceBitmap))
                        {
                            transferredPreparedSourceBitmapOwnership = true;
                        }

                        if (snapshotId != null)
                        {
                            insertSnapshotMarker(snapshotId, "Flatten", true);
                        }

                        await Toast.Make("Paint and Masks applied.").Show();
                    }) ?? Task.CompletedTask);
                }
            });

            return !transferredPreparedSourceBitmapOwnership;
        });
    }

    [RelayCommand]
    private async Task FinishCroppingWithBoundingBox(CanvasCaptureResult result)
    {
        await ExecuteWithPreparedSourceBitmapAsync(result, async (sourceBitmap, _) =>
        {
            await Task.Run(async () =>
            {
                // Re-render the layer using the source bitmap dimensions so masks stay aligned 1:1.
                using var rawMaskBitmap = _canvasActionBitmapService.CreateRenderedLayer(CanvasActions, sourceBitmap.Width, sourceBitmap.Height);

                try
                {
                    var colorizedBitmap = CreatePreparedWorkflowBitmap(sourceBitmap, rawMaskBitmap);
                    using var ownedColorizedBitmap = colorizedBitmap != null && !ReferenceEquals(colorizedBitmap, sourceBitmap)
                        ? colorizedBitmap
                        : null;

                    if (colorizedBitmap == null)
                    {
                        return;
                    }

                    var croppedBitmap = _canvasBitmapService.GetCroppedBitmap(colorizedBitmap, BoundingBox, BoundingBoxScale, BoundingBoxSize);
                    using var ownedCroppedBitmap = croppedBitmap != null && !ReferenceEquals(croppedBitmap, colorizedBitmap)
                        ? croppedBitmap
                        : null;

                    var payload = ImagePayloadHelper.CreateFixedPayload(croppedBitmap, BoundingBoxSize, BoundingBoxSize, PngContentType);
                    if (payload == null)
                    {
                        return;
                    }

                    var parameters = CreateWorkflowNavigationParameters(payload, CreateCroppedWorkflowMaskImageData(rawMaskBitmap));

                    var dispatcher = Shell.Current.CurrentPage?.Dispatcher;
                    await (dispatcher?.DispatchAsync(async () =>
                    {
                        await Shell.Current.GoToAsync("///MainPageTab", parameters);

                        await Toast.Make("Section has been cropped and set as source image.").Show();
                    }) ?? Task.CompletedTask);
                }
                catch
                {
                    // Ignored
                }
            });

            return true;
        });
    }

    private async Task ExecuteWithPreparedSourceBitmapAsync(CanvasCaptureResult result, Func<SKBitmap, SKBitmap?, Task<bool>> workflowAsync)
    {
        IsBusy = true;

        var preparedSourceBitmap = result.PreparedSourceBitmap;
        var sourceBitmap = preparedSourceBitmap ?? SourceBitmap;
        if (sourceBitmap == null)
        {
            preparedSourceBitmap?.Dispose();
            IsBusy = false;
            return;
        }

        var shouldDisposePreparedSourceBitmap = true;

        try
        {
            shouldDisposePreparedSourceBitmap = await workflowAsync(sourceBitmap, preparedSourceBitmap);
        }
        finally
        {
            if (shouldDisposePreparedSourceBitmap)
            {
                preparedSourceBitmap?.Dispose();
            }

            IsBusy = false;
        }
    }

    private SKBitmap? CreatePreparedWorkflowBitmap(SKBitmap sourceBitmap, SKBitmap sameSizeMaskBitmap)
    {
        return _currentCanvasUseMode == CanvasUseMode.Inpaint || _currentCanvasUseMode == CanvasUseMode.PaintOnly
            ? _canvasBitmapService.CreateMaskedBitmap(sourceBitmap, sameSizeMaskBitmap)
            : sourceBitmap;
    }

    private bool ShouldIncludeWorkflowMask()
    {
        return (_currentCanvasUseMode == CanvasUseMode.Inpaint || _currentCanvasUseMode == CanvasUseMode.MaskOnly) &&
            CanvasActions.Any(canvasAction => canvasAction.CanvasActionType == CanvasActionType.Mask);
    }

    private Dictionary<string, object?> CreateWorkflowNavigationParameters(ImageTransferPayload payload, string? maskImageDataString = null)
    {
        return new Dictionary<string, object?>
        {
            { NavigationParams.ImageWidth, payload.Width },
            { NavigationParams.ImageHeight, payload.Height },
            { NavigationParams.InitImgString, payload.ImageDataString },
            { NavigationParams.InitImgThumbnail, payload.ThumbnailString },
            { NavigationParams.MaskImgString, maskImageDataString ?? string.Empty }
        };
    }

    private string CreateWorkflowMaskImageData(SKBitmap rawMaskBitmap)
    {
        if (!ShouldIncludeWorkflowMask() || !ImagePayloadHelper.HasVisiblePixels(rawMaskBitmap))
        {
            return string.Empty;
        }

        using var blackAndWhiteMaskBitmap = _canvasBitmapService.CreateBlackAndWhiteMask(rawMaskBitmap);

        return ImagePayloadHelper.CreateImageDataString(blackAndWhiteMaskBitmap, PngContentType) ?? string.Empty;
    }

    private string CreateCroppedWorkflowMaskImageData(SKBitmap rawMaskBitmap)
    {
        if (!ShouldIncludeWorkflowMask() || !ImagePayloadHelper.HasVisiblePixels(rawMaskBitmap))
        {
            return string.Empty;
        }

        using var blackAndWhiteMaskBitmap = _canvasBitmapService.CreateBlackAndWhiteMask(rawMaskBitmap);
        if (blackAndWhiteMaskBitmap == null)
        {
            return string.Empty;
        }

        var croppedMask = _canvasBitmapService.GetCroppedBitmap(blackAndWhiteMaskBitmap, BoundingBox, BoundingBoxScale, BoundingBoxSize);
        using var ownedCroppedMask = croppedMask != null && !ReferenceEquals(croppedMask, blackAndWhiteMaskBitmap)
            ? croppedMask
            : null;

        if (!ImagePayloadHelper.HasVisiblePixels(croppedMask))
        {
            return string.Empty;
        }

        return ImagePayloadHelper.CreateImageDataString(croppedMask, PngContentType) ?? string.Empty;
    }

    [RelayCommand]
    private async Task PatchAsync()
    {
        ShowActions = false;

        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null || CanvasActions.Count == 0)
        {
            await _popupService.DisplayAlertAsync("Info", "Nothing to patch!", "OK");
            return;
        }

        bool useLastOnly = false;
        var maskCount = CanvasActions.Count(ca => ca.CanvasActionType == CanvasActionType.Mask);

        if (maskCount > 1)
        {
            const string useLastMaskOnlyOption = "Use Last Mask Only";
            const string useAllMasksOption = "Use All Masks";

            var action = await _popupService.DisplayActionSheetAsync("Patch", "Cancel", null, useLastMaskOnlyOption, useAllMasksOption);

            if (action == "Cancel" || string.IsNullOrEmpty(action))
                return;

            useLastOnly = action == useLastMaskOnlyOption;
        }

        var snapshotId = await pushSnapshotAsync("Patch", false);

        try
        {
            IsBusy = true;
            await Task.Delay(100);

            while (SettingSegmentationImage)
            {
                await Task.Delay(100);
            }

            // Unload Segmentation Service to free resource
            _segmentationService.UnloadModel();

            using var mask = await Task.Run(() => _canvasActionBitmapService.CreatePatchMask(CanvasActions, sourceBitmap.Width, sourceBitmap.Height, useLastOnly));

            if (mask != null)
            {
                var result = await _patchService.PatchImageAsync(sourceBitmap, mask);

                if (result != null)
                {
                    if (snapshotId != null)
                    {
                        insertSnapshotMarker(snapshotId, "Patch", false);
                    }
                    PreserveZoomOnNextBitmapChange = true;
                    SourceBitmap = result;
                }
            }

            // Unload Patch Service after use
            _patchService.UnloadModel();
        }
        catch (Exception ex)
        {
            await _popupService.DisplayAlertAsync("Error", $"Patching failed: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SetResolution()
    {
        var sourceBitmap = SourceBitmap;
        if (sourceBitmap == null)
            return;

        var action = await _popupService.DisplayActionSheetAsync("Set Resolution", "Cancel", null, "Create a blank canvas", "Scale Existing Canvas");

        if (action == "Create a blank canvas")
        {
            var parameters = new Dictionary<string, object>
            {
                { NavigationParams.Width, (double)sourceBitmap.Width },
                { NavigationParams.Height, (double)sourceBitmap.Height },
                { NavigationParams.InitImgString, string.Empty }
            };

            var result = await _popupService.ShowPopupForResultAsync("ResolutionSelectPopup", parameters) as IDictionary<string, object>;

            if (result != null)
            {
                if (result.TryGetValue(NavigationParams.Width, out var widthParam) &&
                    double.TryParse(widthParam.ToString(), out var width) &&
                    result.TryGetValue(NavigationParams.Height, out var heightParam) &&
                    double.TryParse(heightParam.ToString(), out var height))
                {
                    ClearSegmentationMask();
                    await clearAllActionsAndHistoryAsync();

                    SourceBitmap?.Dispose();
                    SourceBitmap = new SKBitmap((int)width, (int)height);
                    using (var canvas = new SKCanvas(SourceBitmap))
                    {
                        canvas.Clear(SKColors.WhiteSmoke);
                    }
                    OnPropertyChanged(nameof(SourceBitmap));
                }
            }
        }
        else if (action == "Scale Existing Canvas")
        {
            var parameters = new Dictionary<string, object>
            {
                { NavigationParams.Width, (double)sourceBitmap.Width },
                { NavigationParams.Height, (double)sourceBitmap.Height },
                { NavigationParams.InitImgString, string.Empty }
            };

            var result = await _popupService.ShowPopupForResultAsync("ResolutionSelectPopup", parameters) as IDictionary<string, object>;

            if (result != null)
            {
                if (result.TryGetValue(NavigationParams.Width, out var widthParam) &&
                    double.TryParse(widthParam.ToString(), out var width) &&
                    result.TryGetValue(NavigationParams.Height, out var heightParam) &&
                    double.TryParse(heightParam.ToString(), out var height))
                {
                    ClearSegmentationMask();
                    await clearAllActionsAndHistoryAsync();

                    var resized = sourceBitmap.Resize(new SKImageInfo((int)width, (int)height), new SKSamplingOptions(SKCubicResampler.Mitchell));
                    SourceBitmap?.Dispose();
                    SourceBitmap = resized;
                    OnPropertyChanged(nameof(SourceBitmap));
                }
            }
        }
    }
}