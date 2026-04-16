---
description: 'General work with the Mogri project context'
---

# Mogri

A .NET MAUI mobile app for image generation and editing. Combines on-device processing (masking/patching via ONNX Runtime) with remote server generation (SD Forge Neo / ComfyUI / Comfy Cloud).

For full architecture details, see `docs/Architecture.md`.

## Project Structure

- `Mogri/` — Main MAUI app (Views, ViewModels, Services, Clients, Controls, Models, Registrations)
- `Mogri.Core/` — Shared core library (platform-independent services, interfaces, models, helpers)
- `Mogri.Tests/` — Unit tests (xUnit + Moq)
- `docs/` — Architecture docs, changelog, how-to guides
- `OpenApiSpecs/` — OpenAPI specs for backend APIs (SD Forge Neo, ComfyUI)

## Architecture (MVVM)

- **Views** bind to ViewModel **interfaces** via compiled bindings (`x:DataType`), never implementations
- **ViewModels** coordinate Views ↔ Services — no business logic, no view logic
- **Services** contain business logic — throw exceptions for ViewModels to handle
- **Clients** handle raw HTTP/DTO mapping — never exposed to ViewModels
- **Controls** inherit `ContentView`, use `BindableProperty`, typically no dedicated ViewModels
- **Registrations** in `Mogri/Registrations/` tie interfaces to implementations via DI extension methods

## Key Patterns

- Interface-first: all Services and ViewModels exposed via interfaces
- DI via constructor injection; use `_serviceProvider.GetService<IInterface>()` + `InitWith()` pattern for multiple instances
- CommunityToolkit.Mvvm source generators: `[ObservableProperty]`, `[RelayCommand]`
- Use `IPopupService` instead of `Shell.Current` for alerts, prompts, and action sheets
- SkiaSharp objects (`SKBitmap`, `SKImage`, `SKCanvas`, `SKPaint`) must be disposed — use `using` statements
- Subscribe to singleton service events in `OnNavigatedToAsync`, unsubscribe in `OnNavigatedFromAsync`
- Always dispose `CancellationTokenSource` after cancelling: `cts.Cancel(); cts.Dispose();`

## Naming Conventions

- Interfaces: `IPascalCase` — Classes/Enums/Structs: `PascalCase`
- Public/Protected methods: `PascalCase` — Private methods: `camelCase` — Private fields: `_camelCase`
- Constants: `PascalCase` — Locals/Parameters: `camelCase`
- Async methods always end with `Async` suffix

## Code Style

- One file per class/interface/enum
- Use `var` where the type is obvious
- Avoid conversational comments — comments should explain *why*, not *what*
- Add XML summary comments to classes, interfaces, and enums
- Target 0 warnings and 0 errors