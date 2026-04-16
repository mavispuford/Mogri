---
applyTo: "**/*.xaml"
---

# XAML Rules

- Always set `x:DataType` to the ViewModel **interface** (e.g., `x:DataType="interfaces:IMainPageViewModel"`), never the implementation
- Use compiled bindings — avoid string-based bindings
- Bind to `Command` properties, not event handlers
- Use `IPopupService` for alerts/prompts/action sheets — never `Shell.Current.DisplayAlertAsync` etc.
- Reusable UI components (Controls) inherit `ContentView` and use `BindableProperty` for data input
- Complex view interactions (animations, etc.) should be handled via Behaviors to keep XAML declarative
