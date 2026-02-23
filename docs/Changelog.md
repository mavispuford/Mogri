# Changelog

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
