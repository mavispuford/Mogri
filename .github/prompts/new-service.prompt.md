---
description: 'Create a new service with interface and DI registration'
---

# Create a New Service

Create all files needed for a new service. Ask for the service name and purpose if not provided.

## Files to Create

1. **Interface** at `Mogri/Interfaces/Services/I{Name}Service.cs` (or `Mogri.Core/Interfaces/Services/` if platform-independent)
   - Add XML summary describing its purpose

2. **Implementation** at `Mogri/Services/{Name}Service.cs` (or `Mogri.Core/Services/` if platform-independent)
   - Implement the interface
   - Add XML summary
   - Throw exceptions for error cases — ViewModels handle them
   - If managing heavy resources (ONNX models, native memory), implement `IDisposable`

## Registration to Update

3. Add to `Mogri/Registrations/ServiceRegistrations.cs`:
   ```csharp
   builder.Services.AddSingleton<I{Name}Service, {Name}Service>();
   ```
   Use `AddTransient` only if a new instance is needed per request.

## Reference existing services for patterns (e.g., `ImageService`, `HistoryService`).
