---
description: 'Add a new image generation backend to Mogri'
---

# Add a New Image Generation Backend

Follow the detailed guide in [Docs/HowToAddBackend.md](../../Docs/HowToAddBackend.md).

## Quick Summary

1. **Create service** in `Mogri/Services/` implementing `IImageGenerationBackend`
   - Set the `Name` property (displayed in Settings UI dropdown)
   - Implement all required methods (`InitializeAsync`, `SubmitImageRequestAsync`, etc.)

2. **Register** in `Mogri/Registrations/ServiceRegistrations.cs`:
   ```csharp
   builder.Services.AddSingleton<IImageGenerationBackend, {Name}Service>();
   ```
   `BackendRegistry` and `ProxyImageGenerationService` will automatically pick it up.

3. **Thread safety** — use `CancellationToken` for all async operations, no UI calls from the service

4. **Test** — run the app, go to Settings, select the new backend from the dropdown
