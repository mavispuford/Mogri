using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Models;
using SkiaSharp;

namespace Mogri.Coordinators;

/// <summary>
/// Coordinates segmentation setup, latest-wins cancellation, and interactive mask operations for a canvas session.
/// </summary>
public sealed class CanvasSegmentationCoordinator : ICanvasSegmentationCoordinator
{
    private readonly object _setSegmentationImageLock = new();
    private readonly ICanvasBitmapService _canvasBitmapService;
    private readonly ISegmentationService _segmentationService;

    private CancellationTokenSource? _setSegmentationImageCancellationTokenSource;
    private int _setSegmentationImageRequestCount;
    private int _setSegmentationImageVersion;
    private int _doingSegmentation;
    private bool _hasSegmentationImage;
    private readonly object _rawMaskLock = new();
    private SKBitmap? _rawSegmentationBitmap;

    public CanvasSegmentationCoordinator(ICanvasBitmapService canvasBitmapService, ISegmentationService segmentationService)
    {
        _canvasBitmapService = canvasBitmapService ?? throw new ArgumentNullException(nameof(canvasBitmapService));
        _segmentationService = segmentationService ?? throw new ArgumentNullException(nameof(segmentationService));
    }

    public event EventHandler<CanvasSegmentationImageStateChangedEventArgs>? ImageStateChanged;

    public async Task SetImageAsync(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        CancellationTokenSource? previousSetSegmentationImageCancellationTokenSource;
        CancellationTokenSource currentSetSegmentationImageCancellationTokenSource;
        int currentSetSegmentationImageVersion;

        lock (_setSegmentationImageLock)
        {
            _setSegmentationImageRequestCount++;
            _setSegmentationImageVersion++;
            currentSetSegmentationImageVersion = _setSegmentationImageVersion;

            previousSetSegmentationImageCancellationTokenSource = _setSegmentationImageCancellationTokenSource;
            currentSetSegmentationImageCancellationTokenSource = new CancellationTokenSource();
            _setSegmentationImageCancellationTokenSource = currentSetSegmentationImageCancellationTokenSource;
            _hasSegmentationImage = false;
            lock (_rawMaskLock)
            {
                _rawSegmentationBitmap?.Dispose();
                _rawSegmentationBitmap = null;
            }
        }

        RaiseImageStateChanged(isSettingImage: true, hasSegmentationImage: false);

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
        catch (OperationCanceledException)
        {
            hasSegmentationImage = false;
        }
        finally
        {
            var shouldDisposeCurrentSetSegmentationImageCancellationTokenSource = false;
            var shouldRaiseStateChanged = false;
            var isSettingImage = false;
            var publishedHasSegmentationImage = false;

            lock (_setSegmentationImageLock)
            {
                if (currentSetSegmentationImageVersion == _setSegmentationImageVersion)
                {
                    _hasSegmentationImage = hasSegmentationImage;
                    shouldRaiseStateChanged = true;
                }

                _setSegmentationImageRequestCount--;
                isSettingImage = _setSegmentationImageRequestCount > 0;
                publishedHasSegmentationImage = _hasSegmentationImage;

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

            if (shouldRaiseStateChanged || !isSettingImage)
            {
                RaiseImageStateChanged(isSettingImage, publishedHasSegmentationImage);
            }
        }
    }

    public async Task<CanvasSegmentationMaskUpdateResult?> DoSegmentationAsync(CanvasSegmentationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Points.Length == 0)
        {
            return null;
        }

        if (Interlocked.Exchange(ref _doingSegmentation, 1) == 1)
        {
            return null;
        }

        try
        {
            var maskBitmap = await _segmentationService.DoSegmentation(request.Points);
            if (maskBitmap == null)
            {
                return null;
            }

            SKBitmap paddedBitmap;
            lock (_rawMaskLock)
            {
                if (_rawSegmentationBitmap == null && request.CurrentSegmentationBitmap != null)
                {
                    _rawSegmentationBitmap = request.CurrentSegmentationBitmap.Copy();
                }

                if (_rawSegmentationBitmap == null)
                {
                    _rawSegmentationBitmap = maskBitmap;
                }
                else
                {
                    using var ownedMaskBitmap = maskBitmap;
                    var mergedBitmap = new SKBitmap(_rawSegmentationBitmap.Info);

                    using (var combineCanvas = new SKCanvas(mergedBitmap))
                    using (var paint = new SKPaint { BlendMode = SKBlendMode.SrcOver })
                    {
                        combineCanvas.DrawBitmap(_rawSegmentationBitmap, 0, 0, SKSamplingOptions.Default, paint);
                        paint.BlendMode = request.SegmentationAdd ? SKBlendMode.SrcOver : SKBlendMode.DstOut;
                        combineCanvas.DrawBitmap(maskBitmap, 0, 0, SKSamplingOptions.Default, paint);
                    }

                    _rawSegmentationBitmap.Dispose();
                    _rawSegmentationBitmap = mergedBitmap;
                }

                paddedBitmap = _canvasBitmapService.ApplyMaskPadding(_rawSegmentationBitmap, request.Padding)
                    ?? _rawSegmentationBitmap.Copy();
            }

            return new CanvasSegmentationMaskUpdateResult(paddedBitmap);
        }
        finally
        {
            Interlocked.Exchange(ref _doingSegmentation, 0);
        }
    }

    public Task<SKBitmap?> ApplyPaddingAsync(int padding)
    {
        SKBitmap? rawCopy;
        lock (_rawMaskLock)
        {
            if (_rawSegmentationBitmap == null)
            {
                return Task.FromResult<SKBitmap?>(null);
            }

            rawCopy = _rawSegmentationBitmap.Copy();
        }

        return Task.Run<SKBitmap?>(() =>
        {
            using (rawCopy)
            {
                return _canvasBitmapService.ApplyMaskPadding(rawCopy, padding)
                    ?? rawCopy.Copy();
            }
        });
    }

    public Task<CanvasSegmentationMaskUpdateResult?> InvertMaskAsync(CanvasSegmentationInvertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_rawMaskLock)
        {
            if (_rawSegmentationBitmap == null && request.CurrentSegmentationBitmap == null)
            {
                if (request.SourceImageInfo == null)
                {
                    return Task.FromResult<CanvasSegmentationMaskUpdateResult?>(null);
                }

                var fullMask = new SKBitmap(request.SourceImageInfo.Value);
                using var canvas = new SKCanvas(fullMask);
                canvas.Clear(_segmentationService.MaskColor);

                _rawSegmentationBitmap = fullMask.Copy();
                var paddedFull = _canvasBitmapService.ApplyMaskPadding(_rawSegmentationBitmap, request.Padding)
                    ?? _rawSegmentationBitmap.Copy();

                return Task.FromResult<CanvasSegmentationMaskUpdateResult?>(new CanvasSegmentationMaskUpdateResult(paddedFull));
            }
        }

        return Task.Run<CanvasSegmentationMaskUpdateResult?>(() =>
        {
            SKBitmap invertedBitmap;
            SKBitmap padded;

            lock (_rawMaskLock)
            {
                var sourceToInvert = _rawSegmentationBitmap ?? request.CurrentSegmentationBitmap;
                invertedBitmap = _segmentationService.InvertMask(sourceToInvert!);
                _segmentationService.Reset();

                _rawSegmentationBitmap?.Dispose();
                _rawSegmentationBitmap = invertedBitmap.Copy();

                padded = _canvasBitmapService.ApplyMaskPadding(_rawSegmentationBitmap, request.Padding)
                    ?? _rawSegmentationBitmap.Copy();
            }

            return new CanvasSegmentationMaskUpdateResult(padded);
        });
    }

    public Task<SKBitmap?> CreateMaskBitmapFromSegmentationAsync(SKBitmap segmentationBitmap)
    {
        ArgumentNullException.ThrowIfNull(segmentationBitmap);
        return Task.Run(() => _canvasBitmapService.CreateMaskBitmapFromSegmentationMask(segmentationBitmap));
    }

    public void Reset()
    {
        lock (_rawMaskLock)
        {
            _rawSegmentationBitmap?.Dispose();
            _rawSegmentationBitmap = null;
        }
        _segmentationService.Reset();
    }

    private void RaiseImageStateChanged(bool isSettingImage, bool hasSegmentationImage)
    {
        ImageStateChanged?.Invoke(this, new CanvasSegmentationImageStateChangedEventArgs(isSettingImage, hasSegmentationImage));
    }
}