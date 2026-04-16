# How to Add a New Image Generation Backend

This guide explains how to add support for a new image generation backend (e.g., ComfyUI, Local Diffusion service) to Mogri.

## Architecture

Mogri uses a plugin-style architecture:
1.  **IImageGenerationBackend**: The interface all backends must implement.
2.  **BackendRegistry**: A service that holds all registered backends.
3.  **ProxyImageGenerationService**: The main service consumed by the app, which delegates to the user-selected backend.

## Steps to Add a New Backend

### 1. Implement the Interface

Create a new service class in `Mogri/Services/` that implements `Mogri.Interfaces.Services.IImageGenerationBackend`.

```csharp
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;

namespace Mogri.Services
{
    public class MyNewBackendService : IImageGenerationBackend
    {
        public string Name => "My New Backend"; // Displayed in Settings UI
        public bool Initialized { get; private set; }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            // Connect to your API
            Initialized = true;
        }

        // Implement all other required methods...
        public async IAsyncEnumerable<ApiResponse> SubmitImageRequestAsync(PromptSettings settings, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Yield progress updates and final result
        }
    }
}
```

### 2. Register in MauiProgram.cs

Open `Mogri/Registrations/ServiceRegistrations.cs` (or `MauiProgram.cs` if registered directly) and add your service to the DI container **as an `IImageGenerationBackend`**.

```csharp
// In ServiceRegistrations.cs inside RegisterServices method:

// Existing backends
builder.Services.AddSingleton<IImageGenerationBackend, SdForgeNeoService>();
builder.Services.AddSingleton<IImageGenerationBackend, ComfyUiService>();

// NEW backend
builder.Services.AddSingleton<IImageGenerationBackend, MyNewBackendService>();
```

### 3. Verify Thread Safety

Ensure your service does **not** assume it is running on the Main Thread.
- Use `CancellationToken` for all async operations.
- Do not make UI calls (like `Device.BeginInvokeOnMainThread` or `Shell.Current.DisplayAlert`) inside the service. Exception handling should propagate up or be handled via return values.

### 4. Test

Run the app, go to Settings, and select your new backend from the dropdown. The app will immediately start using it for all generation requests.
