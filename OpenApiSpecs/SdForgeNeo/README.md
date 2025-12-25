# SD Forge Neo Client Generation

This directory contains the OpenAPI specification for the SD Forge Neo API.

## Updating the Client

To update the generated client code (e.g. after downloading a new `openapi.json`), you must first patch the specification to fix type issues, and then regenerate.

1. **Patch the OpenAPI spec** (Fixes `Seed` type to `long`):
   ```bash
   python3 OpenApiSpecs/SdForgeNeo/patch_openapi.py
   ```

2. **Generate the Client**:
   ```bash
   kiota generate -l CSharp -c SdForgeNeoClient -n MobileDiffusion.Clients.SdForgeNeo -d OpenApiSpecs/SdForgeNeo/openapi-patched.json -o MobileDiffusion/Clients/SdForgeNeo
   ```

   *Note: We use `kiota generate` instead of `kiota update` to ensure the lock file points to the patched JSON file.*

## Prerequisites

Ensure you have the Kiota CLI installed:

```bash
dotnet tool install --global Microsoft.Kiota.Tool
```
