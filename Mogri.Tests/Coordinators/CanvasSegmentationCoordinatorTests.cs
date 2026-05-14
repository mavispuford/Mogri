using Moq;
using Mogri.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Models;
using SkiaSharp;
using Xunit;

namespace Mogri.Tests.Coordinators;

public class CanvasSegmentationCoordinatorTests
{
    [Fact]
    public async Task SetImageAsync_WhenLatestRequestWins_PublishesLatestStateAndCancelsPreviousRequest()
    {
        // Arrange
        var canvasBitmapService = new Mock<ICanvasBitmapService>();
        var segmentationService = new Mock<ISegmentationService>();
        using var firstBitmap = CreateBitmap(SKColors.Red);
        using var secondBitmap = CreateBitmap(SKColors.Blue);
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstToken = CancellationToken.None;
        var callCount = 0;
        CanvasSegmentationImageStateChangedEventArgs? lastState = null;

        segmentationService
            .Setup(service => service.SetImage(It.IsAny<SKBitmap>(), It.IsAny<CancellationToken>()))
            .Returns<SKBitmap, CancellationToken>(async (_, token) =>
            {
                callCount++;

                if (callCount == 1)
                {
                    firstToken = token;
                    firstStarted.TrySetResult(true);
                    await releaseFirst.Task;
                    return true;
                }

                secondCompleted.TrySetResult(true);
                return false;
            });

        var coordinator = new CanvasSegmentationCoordinator(canvasBitmapService.Object, segmentationService.Object);
        coordinator.ImageStateChanged += (_, args) => lastState = args;

        // Act
        var firstTask = coordinator.SetImageAsync(firstBitmap);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var secondTask = coordinator.SetImageAsync(secondBitmap);
        await secondCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await secondTask;

        releaseFirst.TrySetResult(true);
        await firstTask;

        // Assert
        Assert.True(firstToken.IsCancellationRequested);
        Assert.NotNull(lastState);
        Assert.False(lastState!.IsSettingImage);
        Assert.False(lastState.HasSegmentationImage);
    }

    [Fact]
    public async Task DoSegmentationAsync_WithExistingMaskAndRemoveMode_RemovesReturnedPixels()
    {
        // Arrange
        var canvasBitmapService = new Mock<ICanvasBitmapService>();
        var segmentationService = new Mock<ISegmentationService>();
        using var currentBitmap = CreateBitmap(SKColors.White);
        using var deltaBitmap = CreateMaskBitmap();

        segmentationService
            .Setup(service => service.DoSegmentation(It.IsAny<SKPoint[]>(), false))
            .ReturnsAsync(deltaBitmap.Copy());

        var coordinator = new CanvasSegmentationCoordinator(canvasBitmapService.Object, segmentationService.Object);
        var request = new CanvasSegmentationRequest
        {
            Points = [new SKPoint(1, 1)],
            CurrentSegmentationBitmap = currentBitmap,
            SegmentationAdd = false
        };

        // Act
        var result = await coordinator.DoSegmentationAsync(request);

        // Assert
        Assert.NotNull(result);
        using var mergedBitmap = result!.SegmentationBitmap;
        Assert.Equal((byte)255, mergedBitmap.GetPixel(0, 0).Alpha);
        Assert.Equal((byte)0, mergedBitmap.GetPixel(1, 1).Alpha);
    }

    [Fact]
    public async Task InvertMaskAsync_WithoutExistingMask_ReturnsFullMaskUsingServiceColor()
    {
        // Arrange
        var canvasBitmapService = new Mock<ICanvasBitmapService>();
        var segmentationService = new Mock<ISegmentationService>();
        segmentationService.SetupGet(service => service.MaskColor).Returns(SKColors.Green);

        var coordinator = new CanvasSegmentationCoordinator(canvasBitmapService.Object, segmentationService.Object);
        var request = new CanvasSegmentationInvertRequest
        {
            CurrentSegmentationBitmap = null,
            SourceImageInfo = new SKImageInfo(2, 2)
        };

        // Act
        var result = await coordinator.InvertMaskAsync(request);

        // Assert
        Assert.NotNull(result);
        using var bitmap = result!.SegmentationBitmap;
        Assert.Equal(SKColors.Green, bitmap.GetPixel(0, 0));
        Assert.Equal(SKColors.Green, bitmap.GetPixel(1, 1));
    }

    [Fact]
    public async Task CreateMaskBitmapFromSegmentationAsync_WithBitmap_DelegatesToCanvasBitmapService()
    {
        // Arrange
        var canvasBitmapService = new Mock<ICanvasBitmapService>();
        var segmentationService = new Mock<ISegmentationService>();
        using var segmentationBitmap = CreateBitmap(SKColors.White);
        using var maskBitmap = CreateMaskBitmap();

        canvasBitmapService
            .Setup(service => service.CreateMaskBitmapFromSegmentationMask(segmentationBitmap))
            .Returns(maskBitmap.Copy());

        var coordinator = new CanvasSegmentationCoordinator(canvasBitmapService.Object, segmentationService.Object);

        // Act
        var result = await coordinator.CreateMaskBitmapFromSegmentationAsync(segmentationBitmap);

        // Assert
        Assert.NotNull(result);
        using var ownedResult = result;
        canvasBitmapService.Verify(service => service.CreateMaskBitmapFromSegmentationMask(segmentationBitmap), Times.Once);
    }

    [Fact]
    public void Reset_WhenCalled_DelegatesToSegmentationServiceReset()
    {
        // Arrange
        var canvasBitmapService = new Mock<ICanvasBitmapService>();
        var segmentationService = new Mock<ISegmentationService>();
        var coordinator = new CanvasSegmentationCoordinator(canvasBitmapService.Object, segmentationService.Object);

        // Act
        coordinator.Reset();

        // Assert
        segmentationService.Verify(service => service.Reset(), Times.Once);
    }

    private static SKBitmap CreateBitmap(SKColor color)
    {
        var bitmap = new SKBitmap(2, 2);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        return bitmap;
    }

    private static SKBitmap CreateMaskBitmap()
    {
        var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.White);
        bitmap.SetPixel(0, 0, SKColors.Transparent);
        return bitmap;
    }
}