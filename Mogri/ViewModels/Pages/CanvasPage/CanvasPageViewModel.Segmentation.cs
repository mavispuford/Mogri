using CommunityToolkit.Mvvm.Input;
using Mogri.Enums;
using Mogri.Models;
using SkiaSharp;

namespace Mogri.ViewModels;

/// <summary>
/// Canvas page view model partial that manages segmentation readiness, interactive segmentation commands, and segmentation mask state.
/// </summary>
public partial class CanvasPageViewModel
{
    private async Task updateSegmentationImageAsync(SKBitmap bitmap)
    {
        await _canvasSegmentationCoordinator.SetImageAsync(bitmap);
    }

    [RelayCommand]
    private async Task DoSegmentation(SKPoint[] points)
    {
        if (points == null ||
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
            IsBusy = true;

            var segmentationResult = await _canvasSegmentationCoordinator.DoSegmentationAsync(new CanvasSegmentationRequest
            {
                Points = points,
                CurrentSegmentationBitmap = SegmentationBitmap,
                SegmentationAdd = SegmentationAdd
            });

            if (segmentationResult != null)
            {
                SegmentationBitmap?.Dispose();
                SegmentationBitmap = segmentationResult.SegmentationBitmap;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await _toastService.ShowAsync("Failed to perform segmentation.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task InvertSegmentationMask()
    {
        if (SegmentationBitmap == null && SourceBitmap == null)
        {
            return;
        }

        try
        {
            IsBusy = true;

            var invertResult = await _canvasSegmentationCoordinator.InvertMaskAsync(new CanvasSegmentationInvertRequest
            {
                CurrentSegmentationBitmap = SegmentationBitmap,
                SourceImageInfo = SourceBitmap?.Info
            });

            if (invertResult != null)
            {
                var oldBitmap = SegmentationBitmap;
                SegmentationBitmap = invertResult.SegmentationBitmap;
                oldBitmap?.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            await _toastService.ShowAsync("Failed to invert mask.");
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

            var maskBitmap = await _canvasSegmentationCoordinator.CreateMaskBitmapFromSegmentationAsync(SegmentationBitmap);
            if (maskBitmap == null)
            {
                return;
            }

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
            await _toastService.ShowAsync("Failed to apply mask.");
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
        _canvasSegmentationCoordinator.Reset();
    }

    [RelayCommand]
    private void ToggleSegmentationAdd()
    {
        SegmentationAdd = !SegmentationAdd;

        _canvasSegmentationCoordinator.Reset();
    }

    private void onSegmentationImageStateChanged(object? sender, CanvasSegmentationImageStateChangedEventArgs e)
    {
        _ = _mainThreadService.InvokeOnMainThreadAsync(() =>
        {
            HasSegmentationImage = e.HasSegmentationImage;
            SettingSegmentationImage = e.IsSettingImage;

            var magicWandTool = AvailableTools.FirstOrDefault(t => t.Type == ToolType.MagicWand);
            if (magicWandTool != null)
            {
                magicWandTool.IsLoading = e.IsSettingImage;
            }
        });
    }
}