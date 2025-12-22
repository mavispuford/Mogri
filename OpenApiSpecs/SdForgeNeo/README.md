# SD Forge Neo Client Generation

This directory contains the OpenAPI specification for the SD Forge Neo API.

## Updating the Client

To update the generated client code after modifying `openapi.json`, run the following command from the root of the repository:

```bash
kiota update --output MobileDiffusion/Clients/SdForgeNeo
```

This command uses the configuration stored in `MobileDiffusion/Clients/SdForgeNeo/kiota-lock.json`.

## Regenerating from Scratch

If you need to regenerate the client from scratch or change configuration options, use the full generate command:

```bash
kiota generate -l CSharp -c SdForgeNeoClient -n MobileDiffusion.Clients.SdForgeNeo -d OpenApiSpecs/SdForgeNeo/openapi.json -o MobileDiffusion/Clients/SdForgeNeo
```

## Prerequisites

Ensure you have the Kiota CLI installed:

```bash
dotnet tool install --global Microsoft.Kiota.Tool
```
