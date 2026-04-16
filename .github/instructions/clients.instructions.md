---
applyTo: "**/Clients/**"
---

# Client Rules

- Handle raw HTTP communication and DTO mapping only
- Wrap generated OpenAPI client code or raw HTTP calls
- Keep API communication logic separate from business logic
- Services consume Clients; Clients should never be exposed to ViewModels
- Map Client DTOs to domain Models before returning to Services
