# Plan: On-Device Inpainting with AOT-GAN

## Overview
This plan describes the steps to replace the existing LaMa inpainting implementation with Qualcomm's AOT-GAN model. This change targets improved performance and reduced model size for mobile devices. The new implementation will reuse the effective cropping and pre-processing logic from the existing service but adapt the inference engine for AOT-GAN.

## Phase 1: Model Analysis & Verification
**Objective**: Confirm the input/output signature of the `aot_gan.onnx` model and verify it loads correctly with its data file.
**Description**: Before writing C# binding code, we must know the exact tensor names and expected shapes (e.g., is it `1x3x512x512`? `1x4x512x512`?). We will use a Python script to inspect the model.
**Outcomes**:
- Confirmed tensor names (inputs/outputs).
- Confirmed input shapes.
- Verified that `aot_gan.onnx` loads using `onnxruntime` in a Python environment.
- **Findings**:
  - Inputs: `image` (1x3x512x512), `mask` (1x1x512x512)
  - Output: `painted_image` (1x3x512x512)
  - Note: The model expects the external data file to be named `model.data`. The file `aot_gan.data` must be renamed to `model.data`.

```markdown
# Phase 1: Model Analysis

## Context
We have `aot_gan.onnx` and `aot_gan.data` in `MobileDiffusion/Resources/Raw`. We need to verify these files and understand their schema before implementing the C# service.

## Objective
Inspect the ONNX model to determine input/output names and shapes.

## Requirements
- Create a Python script `Scripts/inspect_aot_gan.py`.
- The script should load the model using `onnxruntime`.
- Print input names, shapes, and types.
- Print output names, shapes, and types.

## Files to Modify
- `Scripts/inspect_aot_gan.py` (New)
```

## Phase 2: AotGanPatchService Scaffolding
**Objective**: Create the new `AotGanPatchService` class and replicate the cropping logic.
**Description**: We will create a new service class that implements `IPatchService`. We will port over the helper methods `GetBoundingBox` and `GetExpandedCropRect` from `LaMaPatchService` as they are logic-agnostic and work well. We will also set up the ONNX Runtime session initialization.
**Outcomes**:
- `AotGanPatchService.cs` created.
- Cropping/Bounding box logic transplanted.
- `IPatchService` implemented (with empty inference method initially).

```markdown
# Phase 2: AotGanPatchService Scaffolding

## Context
We have verified the model structure. Now we need to build the C# service that will host it. We want to preserve the smart cropping logic from `LaMaPatchService`.

## Objective
Create `AotGanPatchService` and implement the basic infrastructure and cropping logic.

## Requirements
- Create `MobileDiffusion/Services/AotGanPatchService.cs`.
- Implement `IPatchService`.
- Copy `GetBoundingBox` and `GetExpandedCropRect` from `LaMaPatchService`.
- Implement `PatchImageAsync` with the cropping logic (similar to LaMa), but leave the actual model inference call (`RunAotGanModel`) as a placeholder or TODO.
- Implement `UnloadModel`.
- Ensure `InitializeAsync` loads the `aot_gan.onnx` model from the App Package/Raw resources.

## Files to Modify
- `MobileDiffusion/Services/AotGanPatchService.cs` (New)
```

## Phase 3: Inference Implementation
**Objective**: Implement the `RunAotGanModel` method with correct pre/post processing.
**Description**: This is the core logic. We need to convert `SKBitmap` to the `DenseTensor<float>` format AOT-GAN expects, run the inference, and convert the output tensor back to `SKBitmap`.
**Key Considerations**:
- Normalization: AOT-GAN typically expects inputs in `[-1, 1]` or `[0, 1]`. We will start with standard `[0, 1]` and mask logic.
- Mask handling: Ensure the mask channel is correctly formatted (0 vs 1).
- Resource management: Use `using` statements for tensors and results.

```markdown
# Phase 3: Inference Implementation

## Context
The service structure is in place. Now we need to implement the actual ONNX inference logic based on the schema discovered in Phase 1.

## Objective
Implement `RunAotGanModel` to perform the actual inpainting.

## Requirements
- Implement `RunAotGanModel` in `AotGanPatchService`.
- Pre-processing:
  - Resize image/mask to 512x512 (or model input size).
  - Convert pixels to `DenseTensor<float>`.
  - Apply correct normalization (likely `(pixel - 0.5) / 0.5` for [-1,1] or just `pixel/255` for [0,1] depending on model specs - we will assume [0,1] initially or check paper details).
  - **Correction**: AOT-GAN paper often uses [-1, 1]. We will create a flexible implementation or try [-1, 1].
- Inference:
  - Create `NamedOnnxValue` inputs.
  - Run session.
- Post-processing:
  - Convert output tensor back to `SKBitmap`.
  - Denormalize colors.
  - Clamp values.

## Files to Modify
- `MobileDiffusion/Services/AotGanPatchService.cs`
```

## Phase 4: Integration & Switchover
**Objective**: Register the new service and remove the old one.
**Description**: Update the dependency injection container to use `AotGanPatchService` instead of `LaMaPatchService`.
**Outcomes**:
- App uses AOT-GAN for inpainting.
- Old LaMa code is removed.

```markdown
# Phase 4: Integration & Switchover

## Context
The new service is fully implemented. We need to tell the app to use it and clean up the old one.

## Objective
Swap the implementation in DI and remove legacy code.

## Requirements
- Update `MobileDiffusion/Registrations/ServiceRegistrations.cs`:
  - Replace `builder.Services.AddSingleton<IPatchService, LaMaPatchService>();` with `builder.Services.AddSingleton<IPatchService, AotGanPatchService>();`.
- Verify no other direct references to `LaMaPatchService` exist (should only be via interface).
- Delete `MobileDiffusion/Services/LaMaPatchService.cs`.
- (Optional) Verify if any prompt tweaks are needed for the user to download/ensure `aot_gan.data` is included in the build (ensure `MauiAsset` build action).

## Files to Modify
- `MobileDiffusion/Registrations/ServiceRegistrations.cs`
- `MobileDiffusion/Services/LaMaPatchService.cs` (Delete)
```
