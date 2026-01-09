# Implementation Checklist - LaMa Inpainting

- [x] **Phase 1: Service Infrastructure & Scaffolding**
    - [x] Create `IPatchService.cs` interface.
    - [x] Create `LaMaPatchService.cs` class shell.
    - [x] Register `LaMaPatchService` in `ServiceRegistrations.cs`.

- [x] **Phase 2: LaMa Inference Implementation**
    - [x] Implement `PreProcess` logic (Resize 512x512, Normalize).
    - [x] Implement `PatchImageAsync` using `OnnxRuntime`.
    - [x] Implement `PostProcess` logic (Resize back, Clamp).

- [x] **Phase 3: ViewModel Logic & Mask Generation**
    - [x] Inject `IPatchService` into `CanvasPageViewModel`.
    - [x] Implement `GenerateMask` helper method (off-screen rendering).
    - [x] Implement `PatchCommand` with User Choice (Last vs All).
    - [x] Update `SourceBitmap` with result.

- [x] **Phase 4: UI Integration**
    - [x] Add "Bandage" (Patch) button to `CanvasPage.xaml` TitleView.
