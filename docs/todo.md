# Todo List - AOT-GAN Implementation

## Phase 1: Model Analysis
- [x] Create `Scripts/inspect_aot_gan.py`
- [x] Run inspection script and document input/output tensor names

## Phase 2: AotGanPatchService Scaffolding
- [x] Create `MobileDiffusion/Services/AotGanPatchService.cs`
- [x] Port `GetBoundingBox` and `GetExpandedCropRect`
- [x] Implement `IPatchService` structure
- [x] Setup model loading in `InitializeAsync`

## Phase 3: Inference Implementation
- [x] Implement Pre-processing (Resize, Tensor conversion)
- [x] Implement `RunAotGanModel` inference call
- [x] Implement Post-processing (Tensor to Bitmap)

## Phase 4: Integration & Switchover
- [x] Update `ServiceRegistrations.cs` to use `AotGanPatchService`
- [x] Delete `LaMaPatchService.cs`
- [x] Verify functionality
