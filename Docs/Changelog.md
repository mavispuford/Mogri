# Changelog

## 2026-05-06

*This update fixes a Release-only canvas undo bug caused by trim-sensitive persistence code and hardens related local serialization paths.*

### Fixed
- **Flatten Undo Positioning in Release Builds**: Undoing a Flatten action on the canvas now restores previously flattened masks with the correct coordinates in Android Release builds.

### Changed
- **Trim-Safe Canvas Snapshot Persistence**: Canvas snapshot and mask persistence now use centralized source-generated `System.Text.Json` metadata, including explicit handling for `SkiaSharp.SKPoint`.
- **Broader Local Persistence Hardening**: Additional app-owned JSON persistence paths, including presets, checkpoint settings, packaged license data, and PNG metadata storage, were updated to avoid trim-sensitive runtime metadata discovery.
- **Release Validation Guidance**: This bug reinforced that persistence and serialization changes should be validated in Release builds on target devices, not only in Debug emulators.

## 2026-04-11

*This update moves Prompt Styles to fully local, backend-agnostic storage and completes in-app style management.*

### Added
- **Default Prompt Style Seeding**: On startup, the app now seeds five default prompt styles when no local styles exist: Coloring Book, Professional Photo, Cinematic, Watercolor, and 1980s Cartoon.
- **Local Prompt Style Persistence Service**: Added a dedicated LiteDB-backed prompt style service with CRUD and first-run default seeding.

### Changed
- **Prompt Styles Are Backend-Agnostic**: Prompt styles no longer depend on backend APIs and are now always available regardless of selected backend.
- **Prompt Style Management UI**: Users can create, edit, and delete prompt styles directly from the Prompt Style selection flow.

### Removed
- Removed backend prompt style API plumbing (`GetPromptStylesAsync`) and the obsolete backend capability flag for styles.

## 2026-04-05

*This update automates app versioning via CI and exposes the version on the About page.*

### Added
- **Automated Versioning**: App version is now injected at build time by GitHub Actions from git tags. Local builds display `1.0.0-local`.
- **Version Display**: The About page now shows the app version below the logo. Tapping it copies the version to the clipboard.

### Changed
- **GitHub Actions**: Android and iOS workflows now inject `ApplicationDisplayVersion` and `ApplicationVersion` into builds. Artifacts are named with the version.
- **Mogri.csproj**: Version properties are now overridable via MSBuild, with safe local defaults.

## 2026-03-29

*This update adds full undo support for destructive canvas operations by persisting bitmap snapshots to disk, and replaces the Edit Masks popup with a unified Canvas History popup.*

### Added
- **Canvas Undo System**: Destructive operations (Flatten, Stitch, Patch) now save a bitmap snapshot to disk before executing, enabling full undo up to 15 steps deep.
  - Snapshots are stored as PNGs in `CacheDirectory` via `CanvasHistoryService`, keeping memory usage low.
  - Flatten snapshots also preserve the canvas actions list, so undoing a flatten restores both the bitmap and all mask strokes.
- **Canvas History Popup**: Replaced `EditMasksPopup` with `CanvasHistoryPopup`, showing the full action timeline (mask strokes, segmentation masks, and snapshot checkpoints).
  - Added "Clear Masks" button (removes only mask/segmentation actions) alongside the existing "Clear All" (removes everything including snapshots).
  - Snapshot entries display with a distinct icon and are read-only; only the topmost snapshot can be deleted (which triggers undo/restoration).

### Changed
- Flattening masks is no longer permanent and can be undone from the Canvas History.
- Updated the Flatten confirmation dialog to indicate the operation is reversible.
- Patch now skips the "Use Last Mask Only / Use All Masks" action sheet when only one mask exists.
- The Clear command on the canvas now preserves snapshot checkpoints (only clears mask strokes).

### Removed
- Removed `EditMasksPopup`, `EditMasksPopupViewModel`, `EditMaskItemViewModel`, and their interfaces.

## 2026-02-27

*This update significantly improves the user experience during long-running generation tasks by introducing a native Android Foreground Service. Users can now safely navigate away from the app without interrupting the generation process, and track progress directly from their device's notification drawer.*

### Added
- **Background Image Generation (Android)**: Image generation now runs seamlessly in the background via a persistent Foreground Service.
  - Added a persistent notification channel that displays real-time progress percentages while generating.
  - Implemented dynamic updates to the notification for successful completion states and error states without dismissing immediately.
  - Tapping the notification safely returns the user to the `MainPage` to view their finished results.
  - Automatic `PartialWakeLock` handling to prevent the CPU from sleeping and dropping the backend connection while generating in the background.
  - Prompts users for `POST_NOTIFICATIONS` runtime permissions gracefully on Android 13+.
- **Background Service Framework**: Introduced cross-platform task service interfaces (`IGenerationTaskService`) handling events, lifecycles, and results without coupling them to the user visible active UI view model. 

### Changed
- Refactored `MainPageViewModel` generation flow to rely on delegated service events (`ProgressChanged`, `Completed`), allowing the UI to safely detach and reconnect as the view model is navigated away from or destroyed/recreated.

## 2026-02-22

*This update focuses on giving users more granular control over the generation pipeline by exposing VAE and Text Encoder selections. It also introduces automatic model type detection for better default settings and cleans up legacy features like face restoration that are no longer needed for modern models.*

### Added
- **VAE and Text Encoder Selection**: Added the ability to explicitly select which VAE and Text Encoder to use for generation.
  - Added UI pickers in the Prompt Settings page.
  - Implemented fetching available modules from the SD Forge Neo backend.
  - User selections are now saved and restored on a per-model basis.
- **Model Type Detection**: Added automatic detection of the current model type (SD1.5, SDXL, Flux, etc.) from the active backend to apply the correct default generation profiles.

### Changed
- **HiRes Fix (Upscaling)**: Updated SD Forge Neo upscaling logic to only append HiRes Fix parameters when upscaling is enabled, and added support for `HrAdditionalModules` to use the selected VAE/Text Encoder during the upscaling pass.
- Updated OpenAPI specs for SD Forge Neo to the latest version.

### Removed
- **Face Restoration**: Completely removed the old face restoration feature (GFPGAN, CodeFormer) as newer models no longer require it. Removed from UI, models, and backend requests.

## 2026-02-19

*This major update introduces full support for ComfyUI as an alternative image generation backend, allowing runtime switching between SD Forge Neo and ComfyUI. It also implements a robust, custom PNG metadata system for exact round-tripping of generation settings, replacing the fragile regex-based parsing.*

### Added
- **ComfyUI Backend Support**: Added full support for ComfyUI as an image generation backend.
  - Implemented `ComfyUiService` to handle WebSocket-based progress streaming and workflow execution.
  - Added workflow template generation for Text-to-Image, Image-to-Image, and Inpainting.
  - Integrated with `BackendRegistry` allowing runtime switching between SD Forge Neo and ComfyUI.
- **Custom PNG Metadata System**: exact round-trip of generation settings.
  - Implemented `PngMetadataHelper` to read/write a custom `md_settings` chunk containing JSON-serialized `PromptSettings`.
  - Added backward compatibility for reading legacy A1111/Forge `parameters` text chunks.
  - Unified metadata reading across `HistoryService` and backend services.

### Changed
- Updated `Settings` page to include ComfyUI-specific configuration (API Key, etc.).

### Removed
- Removed reliance on fragile regex-based parsing for internal metadata (kept only for legacy support).
