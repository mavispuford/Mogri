# General Architecture Guidelines

These guidelines are for developers and LLMs alike. The goal is to lay out the structure of this app as well as preferred patterns and practices.

## Main Components

This .NET MAUI application follows the MVVM pattern. It consists mainly of these parts:

### Views (Pages, Popups, Controls)
- Registered and tied to a `View Model` interface in `ViewRegistrations.cs`
- These are meant to only contain things pertaining to the visuals of the app
- There is some codebehind code, focused only on view-specific logic (animations, canvas drawing, etc.)
- `Views` should never contain business logic
- `Views` should avoid using `Event Handlers` if possible (bind to `Commands` instead)
- `Views` should always use `x:DataType` (compiled bindings) that point to the `Interface` of the view model, not the implementation
- **Controls**: Reusable UI components (like `ToolControl`) live in the `Controls` namespace. They inherit from `ContentView`, use `BindableProperty` for data input, and generally do not have their own ViewModels.
- **Behaviors**: Complex view interactions (especially animations) are handled via Behaviors (e.g., `CustomAnimationBehavior`) to keep view logic declarative and reusable.
- Popup pages are shown via the `IPopupService`
- Instead of using `Shell.Current` for `DisplayAlertAsync`, `DisplayPromptAsync`, and `DisplayActionSheetAsync`, use `IPopupService`, because its implementation adds additional logic around detecting the topmost page or popup to prevent alert dialogs etc. from displaying underneath popups.

### View Models 
- Exposed via interfaces (ie. `IMainPageViewModel`) and registered in a dependency container in `ViewModelRegistrations.cs`
- This is the glue that ties the views to the business logic, coordinating calls between the view and the services
- The aim is not to contain business logic, but to bind Commands and other properties to the view, and to call the `Services` that actually perform the business logic
- The View Model is also supposed to handle `Exceptions` thrown from the `Services`, and display them in a user friendly way
- For example, the user taps a `Save` button, which executes a `SaveCommand`, which calls a `Save()` medthod on the `View Model`, which calls a `SaveAsync()` method on some `Service`.  The `Service` either succeeds (and returns relevant data to the `ViewModel` class) or throws an exception, which the `View Model` handles in some way (calling a service to display an alert to the user, show/hide a loading spinner, etc.)
- All ViewModels inherit from `BaseViewModel` and `ObservableObject`, leveraging CommunityToolkit source generators (e.g., `[ObservableProperty]`, `[RelayCommand]`) to reduce boilerplate. More specific view models extend other classes as a base. For example,
  - Page view models always extend `PageViewModel`
  - Popup view models always extend `PopupBaseViewModel`
- `View Models` handle navigation between pages and showing/hiding alerts, action sheets, popups, etc.
- `ViewModel` classes should never contain view/business logic

### Services
- Exposed via interfaces (ie. `IFileService`) and registered in a dependency container in `ServiceRegistrations.cs`
- Constructor injected by the `View Models` by their interface, and called by the `View Model`
- Performs business logic and other common code for the `View Model`, such as API calls via HTTP clients, file operations, showing/hiding alert dialogs/action sheets/popups/etc, saving app settings, image segmentation, image operations, and more.
- Services managing heavy resources (like `SegmentationService` with AI models) implement `IDisposable` and are responsible for resource cleanup.
- Services may be stateful singletons (e.g., maintaining connection status or loaded models) and are registered as such.
- Long-running background tasks (like image generation) are abstracted behind cross-platform service interfaces, allowing platform-specific implementations (like Android Foreground Services) to continue running when the app is backgrounded.
- `Services` are expected to throw exceptions when errors happen, leaving it to the `View Model` to handle them gracefully

### Clients
- Located in the `Clients` namespace and folder
- Responsible for raw HTTP communication and DTO mapping
- Wraps generated OpenAPI client code or raw HTTP calls
- Separated from `Services` to keep API communication logic distinct from business logic
- `Services` should consume `Clients`; `Services` should NEVER expose `Client` objects or raw DTOs to the `ViewModel`

### Helpers
- Static classes with bite sized methods (extension methods, etc) that perform shared functionality
- In most cases, code that lives in helper classes could probably live in a service instead

### Registrations
- The `Registrations` folder contains registration classes for Popups, Services, View Models, and Views
- The registrations tie the `Interfaces` to their `Implementations`
- The application currently uses Microsoft's dependency container
- Registrations are implemented as extension methods on `MauiAppBuilder` (e.g., `.RegisterServices()`) to keep `MauiProgram.cs` clean and organized.

### Dependency Injection
- Classes that require things from the dependency container should inject them in their constructor.
- In cases where several instances of a class are required (building up a list, etc.), get them out of the service provider (followed by the `InitWith` pattern) like this:

```csharp
foreach (var item in items) 
{
    // Always pull the interface out of the container
    var viewModel = _serviceProvider.GetService<IExampleViewModel>();

    viewModel.InitWith(item);
}
```

- Example of async variant:

```csharp
foreach (var item in items) 
{
    // Always pull the interface out of the container
    var viewModel = _serviceProvider.GetService<IExampleViewModel>();

    await viewModel.InitWithAsync(item);
}
```

- The above pattern allows async work to happen if necessary, since it is bad practice to do it in the constructor.

### OpenAPI Specs
- The `OpenApiSpecs` folder contains OpenAPI specifications for the APIs that the app targets (for example, the `SdForgeNeo` folder has the openapi specs for SD Forge Neo, and `ComfyUi` for ComfyUI)
- Each OpenAPI spec folder should have a `README.md` file that contains any details about that API spec (gotchas, modifications, commands for code generation, etc.)

## Key Frameworks & Libraries
- **CommunityToolkit.Mvvm**: Heavily used for MVVM implementation (Source Generators for Observables and Commands)
- **SkiaSharp**: Used for high-performance 2D graphics and image manipulation
- **Mopups**: Manages popup windows and modal interactions
- **Microsoft.ML.OnnxRuntime**: Powers the on-device AI inference capabilities

## On-Device Inference
- The application performs hybrid processing: heavy generation on the server, but segmentation/masking locally
- Uses `Microsoft.ML.OnnxRuntime` to run models (like SAM - Segment Anything Model) directly on the device
- Models are loaded from `Resources/Raw` as `.onnx` files
- Inference sessions should be managed carefully to preserve memory and performance

## Platform Specifics
- Platform-specific implementations live in the `Platforms/` folder or within handler mappings in `MauiProgram.cs`
- Platform-specific services (e.g., Android Foreground Services) override default cross-platform implementations in `ServiceRegistrations.cs` using compiler directives (`#if ANDROID`).
- Custom handlers (e.g., removing underlines from Android inputs) are configured in `MauiProgram.cs`

## Configuration & Settings
- App settings (like Server URL) are persisted using native `Preferences`
- `SettingsHelper` provides a strongly-typed wrapper around these preferences

## Builds
- When building, the goal is always to have 0 Warnings and (obviously) 0 Errors.

## Naming
The following naming conventions are used in this application:
- **Interfaces**: `IPascalCase` (e.g., `IImageService`)
- **Classes, Structs, Enums, Events**: `PascalCase` (e.g., `MainPageViewModel`)
- **Properties**: `PascalCase` (e.g., `IsLoading`)
- **Public & Protected Methods**: `PascalCase` (e.g., `InitializeAsync`)
- **Private Methods**: `camelCase` (e.g., `loadItems`)
- **Private Fields**: `_camelCase` with underscore prefix (e.g., `_fileService`)
- **Local Variables & Parameters**: `camelCase` (e.g., `settings`)
- **Constants**: `PascalCase` (e.g., `MaxRetries`)
- **Async Methods**: Always end with `Async` suffix (e.g. `SaveSettingsAsync()`)

## Other Patterns
- One file per class/interface/enum/etc. (avoid having multiple declarations in a single file)
- Enums should also be defined in their own file in the `Enums` folder
- Use `var` in all cases where it makes sense
- Avoid conversational comments. Comments should be useful, explaining why a change was made in a succinct way.
- Add Summary XML comments to classes, interfaces, enums, etc., explaining their purpose at a high level and any caveats to be aware of.

## Branching & Releases

This project follows **GitHub Flow**:

1. **Feature branches** are created from `main` for all changes (e.g., `feature/version-display`, `fix/clipboard-crash`)
2. **Pull Requests** target `main` and are merged via **Squash & Merge** to keep history clean
3. **Releases** are created by tagging `main` with a semver tag (e.g., `v1.2.3`)
   - Tags trigger CI builds that produce versioned Android (signed APK/AAB) and iOS (unsigned IPA) artifacts
   - The tag version is injected into the app binary at build time

### Versioning

- **Display version** (`ApplicationDisplayVersion`): Semantic version matching the git tag (e.g., `1.2.3`). Defaults to `1.0.0-local` for local dev builds.
- **Build number** (`ApplicationVersion`): Auto-incremented by CI using `github.run_number`. Defaults to `1` locally.
- Both are overridable via MSBuild properties: `-p:ApplicationDisplayVersion=x.y.z -p:ApplicationVersion=N`

## Memory Management

### MemoryToolkit.Maui
This project uses [MemoryToolkit.Maui](https://github.com/AdamEssenmacher/MemoryToolkit.Maui) for automated view lifecycle management:
- **`TearDownBehavior.Cascade`** (all builds): Applied in `BasePage`. Automatically clears `BindingContext`, disconnects handlers, and compartmentalizes leaks when pages are popped.
- **`LeakMonitorBehavior.Cascade`** (debug builds only): Applied in `BasePage` under `#if DEBUG`. Detects leaked views at runtime and logs warnings.
- **`UseLeakDetection()`** (debug builds only): Configured in `MauiProgram.cs`. Shows in-app alerts when leaks are detected.
- For Shell tab pages, TearDownBehavior is naturally suppressed around tab switching, allowing tabs to remain alive without premature teardown.

### Event Subscriptions
- Subscribe to singleton service events in lifecycle methods (`OnNavigatedToAsync`), not constructors
- Always unsubscribe in the corresponding teardown method (`OnNavigatedFromAsync`)
- Avoid lambda event handlers on long-lived objects — use named methods so they can be unsubscribed

### IDisposable Resources
- Services holding unmanaged resources (ONNX sessions, native memory) must implement `IDisposable`
- Singleton service disposal happens in `App.xaml.cs` via `Window.Destroying`
- `SkiaSharp` objects (`SKBitmap`, `SKImage`, `SKCanvas`, `SKPaint`) must be disposed — use `using` statements

### Timers
- `System.Threading.Timer` instances must be disposed in page/control lifecycle (`OnDisappearing` or `Unloaded`)
- Always null-check and null-out timer fields after disposal

### CancellationTokenSource
- Always dispose a CTS after cancelling it: `cts.Cancel(); cts.Dispose();`

