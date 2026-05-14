---
applyTo: "**/Services/**"
---

# Service Rules

- Treat `Services` as either leaf capabilities or explicit framework adapter services; do not mix workflow orchestration into them
- Expose via interfaces (e.g., `IImageService`) registered in `ServiceRegistrations.cs`
- Leaf Services contain reusable business logic and throw exceptions for ViewModels or Coordinators to handle
- Framework adapter services wrap MAUI, CommunityToolkit, Mopups, and platform APIs such as Popup, Navigation, Toast, Haptics, page animation, and MainThread
- If a type mostly sequences multiple services, owns workflow state/cancellation, or combines business work with loading/navigation/toast feedback, it belongs in `Coordinators`
- Leaf Services should never make UI calls directly; UI framework access belongs in approved framework adapter services
- Services must not depend on `Coordinators`; service-to-service dependencies should be rare and explicitly justified
- May be stateful singletons registered with `AddSingleton` — manage state carefully
- Services managing heavy resources (ONNX models, native memory) must implement `IDisposable`
- Consume Clients for API communication — never expose Client objects or raw DTOs to ViewModels
- Use `CancellationToken` for all async operations
- Do not assume the main thread unless the service itself is the approved main-thread adapter
