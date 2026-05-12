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

            if (request.CurrentSegmentationBitmap == null)
            {
                return new CanvasSegmentationMaskUpdateResult(maskBitmap);
            }

            using var ownedMaskBitmap = maskBitmap;
            var mergedBitmap = new SKBitmap(request.CurrentSegmentationBitmap.Info);

            using (var combineCanvas = new SKCanvas(mergedBitmap))
            using (var paint = new SKPaint { BlendMode = SKBlendMode.SrcOver })
            {
                combineCanvas.DrawBitmap(request.CurrentSegmentationBitmap, 0, 0, paint);
                paint.BlendMode = request.SegmentationAdd ? SKBlendMode.SrcOver : SKBlendMode.DstOut;
                combineCanvas.DrawBitmap(maskBitmap, 0, 0, paint);
            }

            return new CanvasSegmentationMaskUpdateResult(mergedBitmap);
        }
        finally
        {
            Interlocked.Exchange(ref _doingSegmentation, 0);
        }
    }

    public Task<CanvasSegmentationMaskUpdateResult?> InvertMaskAsync(CanvasSegmentationInvertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CurrentSegmentationBitmap == null)
        {
            if (request.SourceImageInfo == null)
            {
                return Task.FromResult<CanvasSegmentationMaskUpdateResult?>(null);
            }

            var fullMask = new SKBitmap(request.SourceImageInfo.Value);
            using var canvas = new SKCanvas(fullMask);
            canvas.Clear(_segmentationService.MaskColor);

            return Task.FromResult<CanvasSegmentationMaskUpdateResult?>(new CanvasSegmentationMaskUpdateResult(fullMask));
        }

        return Task.Run<CanvasSegmentationMaskUpdateResult?>(() =>
        {
            var invertedBitmap = _segmentationService.InvertMask(request.CurrentSegmentationBitmap);
            _segmentationService.Reset();

            return new CanvasSegmentationMaskUpdateResult(invertedBitmap);
        });
    }

    public Task<SKBitmap?> CreateMaskBitmapFromSegmentationAsync(SKBitmap segmentationBitmap)
    {
        ArgumentNullException.ThrowIfNull(segmentationBitmap);
        return Task.Run(() => _canvasBitmapService.CreateMaskBitmapFromSegmentationMask(segmentationBitmap));
    }

    public void Reset()
    {
        _segmentationService.Reset();
    }

    private void RaiseImageStateChanged(bool isSettingImage, bool hasSegmentationImage)
    {
        ImageStateChanged?.Invoke(this, new CanvasSegmentationImageStateChangedEventArgs(isSettingImage, hasSegmentationImage));
    }
}