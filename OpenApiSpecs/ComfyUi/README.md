# ComfyUI Kiota Client

This directory contains the OpenAPI specification for the ComfyUI API, specifically targeting [Comfy Cloud](https://cloud.comfy.org).

## Regeneration Instructions

To regenerate the C# client code, follow these steps:

1. **Update the Spec**: If the ComfyUI API changes, update `openapi.yaml`.
2. **Patch the Spec**: Run the patch script to generate a Kiota-friendly JSON spec.
   ```bash
   python patch_openapi.py
   ```
3. **Generate Client**: Run Kiota to generate the C# code.
   ```bash
   kiota generate -l CSharp -c ComfyUiClient -n Mogri.Clients.ComfyUi -d openapi-patched.json -o ../../Mogri/Clients/ComfyUi
   ```

## Notes

- The generated client is located in `Mogri/Clients/ComfyUi`.
- The client uses `ApiKeyAuth` for authentication (X-API-Key header).
- WebSocket communication is handled separately, as Kiota only generates REST clients.
