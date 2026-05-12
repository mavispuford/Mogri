using CommunityToolkit.Mvvm.Input;
using Mogri.Enums;
using Mogri.Interfaces.Services;
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
            await _toastService.ShowAsync("There is no image to save.");

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
            await _toastService.ShowAsync("There is no image to send.");

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
            await _toastService.ShowAsync("There is no image data to crop.");

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
            try
            {
                var fileName = await _canvasWorkflowCoordinator.SaveImageAsync(sourceBitmap);
                await _toastService.ShowAsync($"{fileName} saved.");
            }
            catch (Exception)
            {
                await _toastService.ShowAsync("Failed to save image. Please try again.");
            }

            return true;
        });
    }

    [RelayCommand]
    private async Task FinishSendingToImageToImage(CanvasCaptureResult result)
    {
        await ExecuteWithPreparedSourceBitmapAsync(result, async (sourceBitmap, _) =>
        {
            try
            {
                var navigationResult = await _canvasWorkflowCoordinator.CreateImageToImageNavigationAsync(CreateWorkflowRequest(sourceBitmap));
                if (navigationResult != null)
                {
                    await NavigationService.GoToAsync("///MainPageTab", navigationResult.Parameters);
                }
            }
            catch
            {
                // Ignored
            }

            return true;
        });
    }

    [RelayCommand]
    private async Task ApplyPaintAndMasks()
    {
        ShowActions = false;

        if (SourceBitmap == null)
        {
            await _toastService.ShowAsync("There is no image to apply paint/masks to.");
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

            var flattenResult = await _canvasWorkflowCoordinator.ApplyPaintAndMasksAsync(CreateFlattenWorkflowRequest(sourceBitmap, preparedSourceBitmap));
            if (flattenResult != null)
            {
                await _mainThreadService.InvokeOnMainThreadAsync(() =>
                {
                    var oldBitmap = SourceBitmap;
                    SourceBitmap = flattenResult.MergedBitmap;
                    oldBitmap?.Dispose();

                    CanvasActions.Clear();
                    TextElements.Clear();
                    ClearSegmentationMask();

                    transferredPreparedSourceBitmapOwnership = flattenResult.TransfersPreparedSourceBitmapOwnership;

                    if (snapshotId != null)
                    {
                        insertSnapshotMarker(snapshotId, "Flatten", true);
                    }
                });

                await _toastService.ShowAsync("Paint and Masks applied.");
            }

            return !transferredPreparedSourceBitmapOwnership;
        });
    }

    [RelayCommand]
    private async Task FinishCroppingWithBoundingBox(CanvasCaptureResult result)
    {
        await ExecuteWithPreparedSourceBitmapAsync(result, async (sourceBitmap, _) =>
        {
            try
            {
                var navigationResult = await _canvasWorkflowCoordinator.CreateCropNavigationAsync(CreateCropWorkflowRequest(sourceBitmap));
                if (navigationResult != null)
                {
                    await NavigationService.GoToAsync("///MainPageTab", navigationResult.Parameters);
                    await _toastService.ShowAsync("Section has been cropped and set as source image.");
                }
            }
            catch
            {
                // Ignored
            }

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

    private CanvasWorkflowRequest CreateWorkflowRequest(SKBitmap sourceBitmap)
    {
        return new CanvasWorkflowRequest
        {
            SourceBitmap = sourceBitmap,
            CanvasActions = CanvasActions.Cast<ICanvasRenderAction>().ToArray(),
            CanvasUseMode = _currentCanvasUseMode,
            HasMaskActions = HasMaskActions()
        };
    }

    private CanvasCropWorkflowRequest CreateCropWorkflowRequest(SKBitmap sourceBitmap)
    {
        return new CanvasCropWorkflowRequest
        {
            SourceBitmap = sourceBitmap,
            CanvasActions = CanvasActions.Cast<ICanvasRenderAction>().ToArray(),
            CanvasUseMode = _currentCanvasUseMode,
            HasMaskActions = HasMaskActions(),
            BoundingBox = BoundingBox,
            BoundingBoxScale = BoundingBoxScale,
            BoundingBoxSize = BoundingBoxSize
        };
    }

    private CanvasFlattenWorkflowRequest CreateFlattenWorkflowRequest(SKBitmap sourceBitmap, SKBitmap? preparedSourceBitmap)
    {
        return new CanvasFlattenWorkflowRequest
        {
            SourceBitmap = sourceBitmap,
            PreparedSourceBitmap = preparedSourceBitmap,
            CanvasActions = CanvasActions.Cast<ICanvasRenderAction>().ToArray(),
            HasMaskActions = HasMaskActions()
        };
    }

    private CanvasPatchWorkflowRequest CreatePatchWorkflowRequest(SKBitmap sourceBitmap, bool useLastOnly)
    {
        return new CanvasPatchWorkflowRequest
        {
            SourceBitmap = sourceBitmap,
            CanvasActions = CanvasActions.Cast<ICanvasRenderAction>().ToArray(),
            UseLastOnly = useLastOnly
        };
    }

    private bool HasMaskActions()
    {
        return CanvasActions.Any(canvasAction => canvasAction.CanvasActionType == CanvasActionType.Mask);
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

            var patchResult = await _canvasWorkflowCoordinator.PatchAsync(CreatePatchWorkflowRequest(sourceBitmap, useLastOnly));

            if (patchResult?.PatchedBitmap != null)
            {
                if (snapshotId != null)
                {
                    insertSnapshotMarker(snapshotId, "Patch", false);
                }

                PreserveZoomOnNextBitmapChange = true;
                SourceBitmap = patchResult.PatchedBitmap;
            }
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