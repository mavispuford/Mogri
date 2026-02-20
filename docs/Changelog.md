# Changelog

## [Unreleased] - 2026-02-19

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
