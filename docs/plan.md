# LaMa Implementation Refinement Plan

This plan addresses the "odd colors" issue and implements the recommended high-resolution pipeline for the LaMa inpainting service, based on the technical analysis of the quantized ONNX model.

## Overview
We will correct the color channel mismatch (BGR vs RGB) which is causing the immediate visual glitches. Then, we will upgrade the service to use a "Crop-Inpaint-Paste" pipeline (Region of Interest) to support high-resolution editing without downscaling the entire 4K image to 512x512, which would result in quality loss.

## Phases

### Phase 1: Color Space Correction
**Description:** Fix the RGB/BGR channel swapping in `LaMaPatchService` to match the model's expected RGB input/output.
**Expected Outcome:** inpainting results have correct colors, resolving the "blue skin / reversed colors" issues.

```markdown
# Phase 1: Color Space Correction

## Objective
Update `LaMaPatchService` to use RGB channel order for both input tensors and output processing.

## Context
The current implementation assumes BGR order (common in OpenCV), but the analysis confirms the model expects and returns RGB. This causes swapped colors.

## Requirements
1.  **Input Preparation (`LaMaPatchService.cs`)**:
    -   In the pixel iteration loop, assign `rVal` to offset 0, `gVal` to offset 1, and `bVal` to offset 2.
    -   Current code puts `bVal` at offset 0 (Blue) and `rVal` at offset 2 (Red). Swap them back to RGB.
    -   Ensure the normalization remains `pixel / 255.0f`.
2.  **Output Processing (`LaMaPatchService.cs`)**:
    -   When reading from the `outputTensor`, read Red from channel 0, Green from channel 1, and Blue from channel 2.
    -   Current code reads Red from channel 2 and Blue from channel 0. Swap them.
3.  **Validation**:
    -   Ensure `SKSamplingOptions` and bit depth remain consistent.

## Files to Modify
-   `MobileDiffusion/Services/LaMaPatchService.cs`

## Acceptance Criteria
-   Inpainted areas match the color tone of the surrounding image.
-   No "blue tint" or inverted color artifacts.
```

### Phase 2: High-Resolution ROI Pipeline
**Description:** Implement the "Crop-Inpaint-Paste" strategy to handle high-resolution images by only processing the relevant area around the mask, scaling only that crop to 512x512.
**Expected Outcome:** 4K images can be inpainted with high detail; the 512x512 model resolution limit applies only to the patch, not the whole image.

```markdown
# Phase 2: High-Resolution ROI Pipeline

## Objective
Refactor `InpaintImageAsync` to crop the masked region before inference, preserving the original image resolution.

## Context
Downscaling a full 4K image to 512x512 for inference destroys detail. We need to crop a 512x512 equivalent square around the mask, inpaint that, and paste it back.

## Requirements
1.  **Bounding Box Calculation**:
    -   Analyze the `mask` bitmap to find the bounding box `(minX, minY, maxX, maxY)` of all non-black pixels.
    -   If mask is empty, return original image.
2.  **Padding/Dilation**:
    -   Expand the bounding box by 3x (or as much as fits) to provide context for the model. 
    -   Ensure the crop is a **Square** to minimize aspect ratio distortion during resize.
    -   Clamp to image bounds.
3.  **Crop & Resize**:
    -   Crop the source image and the mask using the calculated square.
    -   Resize the cropped source and cropped mask to **512x512**.
4.  **Inference (Existing Logic)**:
    -   Pass the 512x512 crops to the existing ONNX inference logic.
    -   Receive 512x512 output.
5.  **Composite Back**:
    -   Resize the 512x512 output back to the **original crop dimensions**.
    -   Paste/Draw this result onto the original high-res image at the crop origin.
    -   Consider feathering/blending edges if possible (optional for now, but good for "working as expected").
6.  **Refactoring**:
    -   The core ONNX logic can remain, but the wrapping steps in `PatchImageAsync` need to change.

## Files to Modify
-   `MobileDiffusion/Services/LaMaPatchService.cs`

## Acceptance Criteria
-   Inpainting a small area on a large image results in a high-resolution patch.
-   The rest of the image remains untouched and full resolution.
```
