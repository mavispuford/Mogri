using Mogri.Models;
using SkiaSharp;

namespace Mogri.Interfaces.Coordinators;

/// <summary>
/// Coordinates canvas save, payload-preparation, flatten, crop, and patch workflows.
/// </summary>
public interface ICanvasWorkflowCoordinator
{
    Task<string> SaveImageAsync(SKBitmap sourceBitmap);

    Task<CanvasWorkflowNavigationResult?> CreateImageToImageNavigationAsync(CanvasWorkflowRequest request);

    Task<CanvasWorkflowNavigationResult?> CreateCropNavigationAsync(CanvasCropWorkflowRequest request);

    Task<CanvasFlattenWorkflowResult?> ApplyPaintAndMasksAsync(CanvasFlattenWorkflowRequest request);

    Task<CanvasPatchWorkflowResult?> PatchAsync(CanvasPatchWorkflowRequest request);
}