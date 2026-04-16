---
applyTo: "**/ViewModels/**"
---

# ViewModel Rules

- All ViewModels inherit from `BaseViewModel` (→ `ObservableObject`)
- Page ViewModels extend `PageViewModel`; Popup ViewModels extend `PopupBaseViewModel`
- Use CommunityToolkit.Mvvm source generators: `[ObservableProperty]`, `[RelayCommand]`
- ViewModels must NOT contain business logic or view logic — they coordinate between Views and Services
- Handle exceptions thrown by Services and display user-friendly messages via `IPopupService`
- Expose via interfaces (e.g., `IMainPageViewModel`) registered in `ViewModelRegistrations.cs`
- Subscribe to singleton service events in `OnNavigatedToAsync`, unsubscribe in `OnNavigatedFromAsync`
- Use named methods (not lambdas) for event handlers on long-lived objects so they can be unsubscribed
- For multiple ViewModel instances, use `_serviceProvider.GetService<IInterface>()` + `InitWith()` / `InitWithAsync()`
