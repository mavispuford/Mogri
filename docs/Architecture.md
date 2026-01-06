# General Architecture Guidelines

## Main Components

This .NET MAUI application follows the MVVM pattern. It consists mainly of these parts:

### Views (Pages, Popups, Controls)
- Registered and tied to a `View Model` interface in `ViewRegistrations.cs`
- These are meant to only contain things pertaining to the visuals of the app
- There is some codebehind code, focused only on view-specific logic (animations, canvas drawing, etc.)
- `Views` should never contain business logic
- `Views` should avoid using `Event Handlers` if possible (bind to `Commands` instead)

### View Models 
- Exposed via interfaces (ie. `IMainPageViewModel`) and registered in a dependency container in `ViewModelRegistrations.cs`
- This is the glue that ties the views to the business logic, coordinating calls between the view and the services
- The aim is not to contain business logic, but to bind Commands and other properties to the view, and to call the `Services` that actually perform the business logic
- The View Model is also supposed to handle `Exceptions` thrown from the `Services`, and display them in a user friendly way
- For example, the user taps a `Save` button, which executes a `SaveCommand`, which calls a `Save()` medthod on the `View Model`, which calls a `SaveAsync()` method on some `Service`.  The `Service` either succeeds (and returns relevant data to the `ViewModel` class) or throws an exception, which the `View Model` handles in some way (calling a service to display an alert to the user, show/hide a loading spinner, etc.)
- `View Models` handle navigation between pages and showing/hiding alerts, action sheets, popups, etc.
- `ViewModel` classes should never contain view/business logic

### Services
- Exposed via interfaces (ie. `IFileService`) and registered in a dependency container in `ServiceRegistrations.cs`
- Constructor injected by the `View Models` by their interface, and called by the `View Model`
- Performs business logic and other common code for the `View Model`, such as API calls via HTTP clients, file operations, showing/hiding alert dialogs/action sheets/popups/etc, saving app settings, image segmentation, image operations, and more.
- `Services` are expected to throw exceptions when errors happen, leaving it to the `View Model` to handle them gracefully

### Helpers
- Static classes with bite sized methods (extension methods, etc) that perform shared functionality
- In most cases, code that lives in helper classes could probably live in a service instead

### Registrations
- The `Registrations` folder contains registration classes for Popups, Services, View Models, and Views
- The application currently uses Microsoft's dependency container

### OpenAPI Specs
- The `OpenApiSpecs` folder contains OpenAPI specifications for the APIs that the app targets (for example, the `SdForgeNeo` folder has the openapi specs for SD Forge Neo)
- Each OpenAPI spec folder should have a `README.md` file that contains any details about that API spec (gotchas, modifications, commands for code generation, etc.)