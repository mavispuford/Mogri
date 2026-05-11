using Mogri.Interfaces.Services;
using Mogri.Services;
using SkiaSharp;
using Xunit;

namespace Mogri.Tests.Services;

public class CanvasActionBitmapServiceTests
{
    [Fact]
    public void CreateRenderedLayer_WithNullActions_ReturnsTransparentBitmap()
    {
        // Arrange
        ICanvasActionBitmapService service = CreateService();

        // Act
        using var result = service.CreateRenderedLayer(null, 2, 1);

        // Assert
        Assert.Equal((byte)0, result.GetPixel(0, 0).Alpha);
        Assert.Equal((byte)0, result.GetPixel(1, 0).Alpha);
    }

    [Fact]
    public void CreateRenderedLayer_WithRenderableAction_UsesSavingRenderMode()
    {
        // Arrange
        ICanvasActionBitmapService service = CreateService();
        var actions = new ICanvasRenderAction[]
        {
            new SavingAwareRenderAction(SKColors.Red)
        };

        // Act
        using var result = service.CreateRenderedLayer(actions, 1, 1);

        // Assert
        Assert.Equal(SKColors.Red, result.GetPixel(0, 0));
    }

    [Fact]
    public void CreatePatchMask_WithEmptyActions_ReturnsBlackBackground()
    {
        // Arrange
        ICanvasActionBitmapService service = CreateService();

        // Act
        using var result = service.CreatePatchMask(Array.Empty<ICanvasRenderAction>(), 2, 1, false);

        // Assert
        Assert.Equal(SKColors.Black, result.GetPixel(0, 0));
        Assert.Equal(SKColors.Black, result.GetPixel(1, 0));
    }

    [Fact]
    public void CreatePatchMask_WithBitmapMaskAction_ReturnsWhiteMaskPixels()
    {
        // Arrange
        using var bitmap = CreateBitmap(1, 1, SKColors.Blue);
        ICanvasActionBitmapService service = CreateService();
        var actions = new ICanvasRenderAction[]
        {
            new BitmapMaskAction(bitmap)
        };

        // Act
        using var result = service.CreatePatchMask(actions, 1, 1, false);

        // Assert
        Assert.Equal(SKColors.White, result.GetPixel(0, 0));
    }

    [Fact]
    public void CreatePatchMask_WithUseLastOnly_RendersOnlyLastAction()
    {
        // Arrange
        using var firstBitmap = CreateBitmap(2, 1, SKColors.Transparent);
        using var secondBitmap = CreateBitmap(2, 1, SKColors.Transparent);
        firstBitmap.SetPixel(0, 0, SKColors.Blue);
        secondBitmap.SetPixel(1, 0, SKColors.Blue);

        ICanvasActionBitmapService service = CreateService();
        var actions = new ICanvasRenderAction[]
        {
            new BitmapMaskAction(firstBitmap),
            new BitmapMaskAction(secondBitmap)
        };

        // Act
        using var result = service.CreatePatchMask(actions, 2, 1, true);

        // Assert
        Assert.Equal(SKColors.Black, result.GetPixel(0, 0));
        Assert.Equal(SKColors.White, result.GetPixel(1, 0));
    }

    [Fact]
    public void CreatePatchMask_WithEraseStrokeAction_ClearsPreviouslyPaintedMaskPixels()
    {
        // Arrange
        ICanvasActionBitmapService service = CreateService();
        var actions = new ICanvasRenderAction[]
        {
            new StrokeMaskAction(true, 1f, new SKPoint(0, 1), new SKPoint(4, 1)),
            new StrokeMaskAction(false, 1f, new SKPoint(2, 1), new SKPoint(4, 1))
        };

        // Act
        using var result = service.CreatePatchMask(actions, 5, 3, false);

        // Assert
        Assert.True(result.GetPixel(1, 1).Red > result.GetPixel(3, 1).Red);
    }

    private static ICanvasActionBitmapService CreateService()
    {
        return new CanvasActionBitmapService();
    }

    private static SKBitmap CreateBitmap(int width, int height, SKColor fillColor)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(fillColor);

        return bitmap;
    }

    private sealed class SavingAwareRenderAction(SKColor color) : ICanvasRenderAction
    {
        public void Execute(SKCanvas canvas, SKImageInfo imageInfo, bool isSaving)
        {
            using var paint = new SKPaint
            {
                Color = isSaving ? color : SKColors.Transparent
            };

            canvas.DrawPoint(0, 0, paint);
        }
    }

    private sealed class BitmapMaskAction(SKBitmap bitmap) : ICanvasBitmapMaskAction
    {
        public SKBitmap? Bitmap { get; } = bitmap;

        public void Execute(SKCanvas canvas, SKImageInfo imageInfo, bool isSaving)
        {
        }
    }

    private sealed class StrokeMaskAction(bool addsToMask, float brushSize, params SKPoint[] points) : ICanvasMaskStrokeAction
    {
        public float BrushSize { get; } = brushSize;

        public bool AddsToMask { get; } = addsToMask;

        public IReadOnlyList<SKPoint> Points { get; } = points;

        public void Execute(SKCanvas canvas, SKImageInfo imageInfo, bool isSaving)
        {
        }
    }
}