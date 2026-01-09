# Implementation Plan - LaMa Inpainting Service

This plan outlines the steps to integrate the LaMa (Large Mask Inpainting) ONNX model into MobileDiffusion to provide a fast and high-quality image patching feature.

## Overview
We will create a new `IPatchService` implemented by `LaMaPatchService` to handle the ONNX inference. We will then update the `CanvasPageViewModel` to generate masks from existing drawing actions (optimizing for "Last Mask" vs "All Masks" scenarios) and consume this service. Finally, we will expose this functionality via a new button in the `CanvasPage` UI.

## Phases

### Phase 1: Service Infrastructure & Scaffolding
**Description:** Define the `IPatchService` interface and create the initial `LaMaPatchService` class shell. Register the service in the dependency injection container.
**Expected Outcome:** The application compiles with the new service structure in place, ready for logic implementation.

```markdown
# Phase 1: Service Infrastructure & Scaffolding

## Objective
Create the foundation for the patching feature by defining the service interface and registering the concrete implementation.

## Context
We are adding a new `LaMaPatchService` to handle image inpainting using an ONNX model.

## Requirements
1.  **Create Interface**: `MobileDiffusion/Interfaces/Services/IPatchService.cs`
    -   Should define a method `Task<SKBitmap> PatchImageAsync(SKBitmap image, SKBitmap mask);`
2.  **Create Service**: `MobileDiffusion/Services/LaMaPatchService.cs`
    -   Implement `IPatchService`.
    -   Return the input `image` for now (placeholder) or throw `NotImplementedException`.
3.  **Register Service**: `MobileDiffusion/Registrations/ServiceRegistrations.cs`
    -   Register `IPatchService` with `LaMaPatchService`. use `AddSingleton` or `AddTransient` as appropriate (Singleton is likely best if we want to cache the model session, but ensure thread safety; otherwise Transient). Let's use **Singleton** to load the model once.

## Files to Modify
-   `MobileDiffusion/Interfaces/Services/IPatchService.cs` (New)
-   `MobileDiffusion/Services/LaMaPatchService.cs` (New)
-   `MobileDiffusion/Registrations/ServiceRegistrations.cs`

## Acceptance Criteria
-   The solution builds without errors.
-   `IPatchService` can be injected into constructors.
```

### Phase 2: LaMa Inference Implementation
**Description:** Implement the core logic within `LaMaPatchService` to load the ONNX model, preprocess images (resize/normalize), run inference, and postprocess the output.
**Expected Outcome:** `LaMaPatchService` correctly consumes `lama_int8.onnx` and returns an inpainted image.

```markdown
# Phase 2: LaMa Inference Implementation

## Objective
Implement the actual ONNX inference logic inside `LaMaPatchService`.

## Context
The service structure exists. We have `lama_int8.onnx` in `Resources/Raw`. We need to use `Microsoft.ML.OnnxRuntime`.

## Requirements
1.  **Load Model**: In `LaMaPatchService` constructor (or lazy load), load the model from `Resources/Raw/lama_int8.onnx`.
2.  **Preprocessing**: Implement `PreProcess(SKBitmap image, SKBitmap mask)`.
    -   **Resize**: STRICTLY resize both to **512x512**. The ONNX model has a fixed input shape.
    -   **Normalize**: 
        -   Image: Convert to float32 tensor. Typical LaMa preprocessing is `(image / 127.5) - 1.0` (Range [-1, 1]) OR simply `image / 255.0` (Range [0, 1]). We will try `image / 255.0` first as it's common for ONNX exports, but be prepared to switch to [-1, 1].
        -   Mask: Convert to float32 tensor (0.0 for background, 1.0 for masked area).
    -   **Tensor Shape**: Create dense tensors with dimensions `[1, 3, 512, 512]` for image and `[1, 1, 512, 512]` for mask.
3.  **Inference**: Run the `InferenceSession`.
    -   **Inputs**:
        -   Name: `image`, Shape: `[1, 3, 512, 512]`, Type: Float32
        -   Name: `mask`, Shape: `[1, 1, 512, 512]`, Type: Float32
    -   **Outputs**:
        -   Name: `output`, Shape: `[1, 3, 512, 512]`, Type: Float32
4.  **Postprocessing**:
    -   **Resize**: Scale the 512x512 output back to the *original* bounding box or image size.
    -   **Composite**: Draw the inpainted result *only* onto the masked area of the original image to preserve the unmasked details (pixel perfect).
5.  **Files to Modify**:
    -   `MobileDiffusion/Services/LaMaPatchService.cs`

## Technical Details
-   Use `SkiaSharp` for resizing.
-   Use `Microsoft.ML.OnnxRuntime.Tensors` for tensor creation.
-   Input shape is typically (1, 3, 512, 512) for image and (1, 1, 512, 512) for mask.
-   Ensure proper disposal of `InferenceSession` if the class is disposable, or keep it alive as Singleton.

## Acceptance Criteria
-   `PatchImageAsync` takes an image and mask and returns a patched image.
```

### Phase 3: ViewModel Logic & Mask Generation
**Description:** Implement the logic in `CanvasPageViewModel` to handle user interaction, generate the appropriate mask (Last vs All), and call the service.
**Expected Outcome:** The ViewModel can handle the patching flow, prompting the user and interacting with the service.

```markdown
# Phase 3: ViewModel Logic & Mask Generation

## Objective
Integrate the patching logic into the ViewModel, enabling mask generation from drawing history.

## Context
The service is ready. We need to invoke it from the `CanvasPageViewModel`.

## Requirements
1.  **Inject Service**: Add `IPatchService` to `CanvasPageViewModel` constructor.
2.  **Add Command**: `PatchCommand`.
3.  **User Interaction**:
    -   When `PatchCommand` executes, display a dialog (ActionSheet or Alert) asking: "Patch Options": "Use Last Mask Only", "Use All Masks", "Cancel".
4.  **Mask Generation**:
    -   Implement a private helper `GenerateMask(bool useLastOnly)`.
    -   This should create an off-screen `SKCanvas` of the same size as `SourceBitmap`.
    -   Iterate through `CanvasActions`.
    -   If `useLastOnly`, only draw the very last action.
    -   If `Use All`, draw all actions.
    -   Draw white on black background (or appropriate mask colors for LaMa).
5.  **Execution**:
    -   Call `_patchService.PatchImageAsync(SourceBitmap, generatedMask)`.
    -   Update `SourceBitmap` with the result.
    -   **Important**: Do NOT clear `CanvasActions`. The user wants to keep the history (maybe to undo the patch or add more). *Clarification*: If we update `SourceBitmap`, the underlying image changes. If we keep mask layers on top, they will still obscure the patch. The user said: "without clearing the current masks or CanvasActions". This implies the masks stay *visible* on top? Or does the user mean "don't delete the history"? Usually, after patching, you want to see the result. If the mask stays on top, you can't see the fix.
    -   *Interpretation*: The user likely wants to "Commit" the patch to the base image but keep the *ability* to undo or modify. However, strictly follow "without clearing...". If the mask covers the patch, the user will physically clear it manually or undo. We will just set `SourceBitmap`.
6.  **Files to Modify**:
    -   `MobileDiffusion/ViewModels/Pages/CanvasPageViewModel.cs`

## Acceptance Criteria
-   User is prompted for mask selection.
-   Correct mask is generated based on selection.
-   `SourceBitmap` is updated after logical processing.
```

### Phase 4: UI Integration
**Description:** Add the trigger button to the UI.
**Expected Outcome:** A new bandage icon button is visible and functional in the CanvasPage header.

```markdown
# Phase 4: UI Integration

## Objective
Add the "Patch" button to the application toolbar.

## Context
ViewModel has `PatchCommand`.

## Requirements
1.  **Update XAML**: `MobileDiffusion/Views/CanvasPage.xaml`
2.  **Add Button**: inside `Shell.TitleView`'s `HorizontalStackLayout`.
    -   Icon: Bandage glyph "e3f3" (MaterialIcons).
    -   Command: `{Binding PatchCommand}`.
    -   ToolTip: "Patch / Inpaint".
    -   Placement: Probably near the "Save Mask" or "Use as Source" buttons.

## Files to Modify
-   `MobileDiffusion/Views/CanvasPage.xaml`

## Acceptance Criteria
-   Button appears in the title bar.
-   Clicking it triggers the ViewModel logic.
```
