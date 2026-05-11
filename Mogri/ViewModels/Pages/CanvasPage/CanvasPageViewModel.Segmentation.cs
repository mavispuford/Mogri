using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.Input;
using Mogri.Enums;
using SkiaSharp;

namespace Mogri.ViewModels;

/// <summary>
/// Canvas page view model partial that manages segmentation readiness, interactive segmentation commands, and segmentation mask state.
/// </summary>
public partial class CanvasPageViewModel
{
    private async Task updateSegmentationImageAsync(SKBitmap bitmap)
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
}