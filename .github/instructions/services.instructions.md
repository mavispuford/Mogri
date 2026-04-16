---
applyTo: "**/Services/**"
---

# Service Rules

- Expose via interfaces (e.g., `IImageService`) registered in `ServiceRegistrations.cs`
- Services contain business logic — throw exceptions for ViewModels to handle
- Never make UI calls (alerts, navigation, etc.) from Services
- May be stateful singletons registered with `AddSingleton` — manage state carefully
- Services managing heavy resources (ONNX models, native memory) must implement `IDisposable`
- Consume Clients for API communication — never expose Client objects or raw DTOs to ViewModels
- Use `CancellationToken` for all async operations
- Do not assume the main thread — avoid UI thread calls
