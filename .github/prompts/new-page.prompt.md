---
description: 'Scaffold a new MAUI page with ViewModel, Interface, and registrations'
---

# Create a New Page

Create all files needed for a new MAUI page. Ask for the page name if not provided.

## Files to Create

1. **Interface** at `Mogri/Interfaces/ViewModels/Pages/I{Name}PageViewModel.cs`
   - Inherit from `IPageViewModel`
   - Add XML summary comment

2. **ViewModel** at `Mogri/ViewModels/Pages/{Name}PageViewModel.cs`
   - Inherit from `PageViewModel`
   - Implement `I{Name}PageViewModel`
   - Use CommunityToolkit source generators (`[ObservableProperty]`, `[RelayCommand]`)
   - Add XML summary comment

3. **View (XAML)** at `Mogri/Views/{Name}Page.xaml`
   - Set `x:DataType` to the ViewModel **interface**
   - Follow the layout patterns of existing pages

4. **View (Code-behind)** at `Mogri/Views/{Name}Page.xaml.cs`
   - Inherit from `BasePage`

## Registrations to Update

5. Add to `Mogri/Registrations/ViewModelRegistrations.cs`:
   ```csharp
   builder.Services.AddTransient<I{Name}PageViewModel, {Name}PageViewModel>();
   ```

6. Add to `Mogri/Registrations/ViewRegistrations.cs`:
   ```csharp
   registerPage<I{Name}PageViewModel, {Name}Page>(builder.Services, () => new {Name}Page());
   ```

## Reference existing pages for patterns (e.g., `AboutPage`, `HistoryPage`).
