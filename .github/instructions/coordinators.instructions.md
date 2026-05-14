---
applyTo: "**/Coordinators/**"
---

# Coordinator Rules

- Expose coordinators via interfaces under `Interfaces/Coordinators`
- Coordinators own multi-step workflows that sequence `Services`, `Clients`, `Helpers`, and approved framework adapter services
- Use a coordinator when logic would otherwise chain multiple services, own workflow or cancellation state, switch between backends, or combine business work with loading/navigation/toast feedback
- Coordinators must not depend on `Views`, `ViewModels`, or controls
- Coordinators should not call `Shell.Current`, `Toast.Make`, `HapticFeedback`, page animation APIs, `MainThread`, or `Dispatcher` directly; use adapter services instead
- Prefer explicit request/result models for non-trivial workflows rather than mutating `ViewModel` state implicitly
- Keep reusable leaf logic in `Services` or `Helpers`; coordinators orchestrate, they do not absorb every implementation detail
- Throw exceptions or return explicit results so `ViewModels` can update bound state and present user-facing responses predictably