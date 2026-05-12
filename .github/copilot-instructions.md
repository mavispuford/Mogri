---
description: 'General work with the Mogri project context'
---

# Mogri

A .NET MAUI mobile app for image generation and editing. Combines on-device processing (masking/patching via ONNX Runtime) with remote server generation (SD Forge Neo / ComfyUI / Comfy Cloud).

For full architecture details, see `Docs/Architecture.md`.

## Project Structure

- `Mogri/` — Main MAUI app (Views, ViewModels, Services, Clients, Controls, Models, Registrations)
- `Mogri.Core/` — Shared core library (platform-independent services, interfaces, models, helpers)
- `Mogri.Tests/` — Unit tests (xUnit + Moq)
- `Docs/` — Architecture docs, changelog, how-to guides
- `OpenApiSpecs/` — OpenAPI specs for backend APIs (SD Forge Neo, ComfyUI)

## Architecture (MVVM)

- **Views** bind to ViewModel **interfaces** via compiled bindings (`x:DataType`), never implementations
- **ViewModels** coordinate Views with leaf Services, Coordinators, and approved framework adapter services — no business logic, no view logic
- **Coordinators** own multi-step workflows by composing Services, Clients, Helpers, and approved framework adapter services
- **Services** stay focused on leaf capabilities; orchestration-heavy types should move to Coordinators
- **Framework adapter services** wrap UI/runtime APIs such as Popup, Navigation, Toast, Haptics, page animation, and MainThread so non-view layers avoid direct framework calls
- **Clients** handle raw HTTP/DTO mapping — never exposed to ViewModels
- **Controls** inherit `ContentView`, use `BindableProperty`, typically no dedicated ViewModels
- **Registrations** in `Mogri/Registrations/` tie interfaces to implementations via DI extension methods

## Key Patterns

- Interface-first: Services, Coordinators, and ViewModels are exposed via interfaces
- DI via constructor injection; use `_serviceProvider.GetService<IInterface>()` + `InitWith()` pattern for multiple instances
- CommunityToolkit.Mvvm source generators: `[ObservableProperty]`, `[RelayCommand]`
- Use approved framework adapter services instead of direct `Shell.Current`, `Toast.Make`, `HapticFeedback`, page animation APIs, or `MainThread`/`Dispatcher` usage in non-view layers
- `IPopupService` is the existing adapter; navigation/toast/haptics/animation/main-thread adapters follow the same pattern in the app architecture
- If a class mostly sequences multiple services or mixes business work with navigation/loading/toast feedback, prefer a Coordinator over another Service
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