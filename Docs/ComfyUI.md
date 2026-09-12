# ComfyUI

Mogri supports ComfyUI as a generation backend, either through a local server or Comfy Cloud. Mogri builds API-format workflows for each request and submits them to ComfyUI; it does not import arbitrary ComfyUI workflow JSON files.

This page describes the ComfyUI-specific setup, supported features, resource discovery, and known limitations. General app setup is covered in the [main README](../README.md).

## Quick Setup

1. Install ComfyUI and the model resources required by the model family you want to use.
2. For a local server, start ComfyUI with `--listen` so the phone can reach it over the local network. Use `--port` if you need a port other than the default `8188`.
3. In Mogri, open Settings, select **ComfyUI**, and enter the server URL, for example `http://192.168.1.x:8188`.
4. Use Mogri's resource refresh action after installing models, LoRAs, VAEs, text encoders, or upscalers.

For the network configuration details on the ComfyUI side, see the [ComfyUI LAN guide](https://comfyui-wiki.com/en/faq/how-to-access-comfyui-on-lan).

Mogri can also send an optional custom authentication header to a local ComfyUI server. Comfy Cloud uses the API key configured for the Comfy Cloud backend.

## Supported Workflows

Mogri generates the following ComfyUI workflows:

| Workflow | Behavior |
| --- | --- |
| Text to image | Loads the selected checkpoint or diffusion model, encodes the positive and negative prompts, samples a new latent image, decodes it, and saves the result. |
| Image to image | Uploads the source image, encodes it with the selected VAE, and uses the denoising strength during sampling. |
| Inpainting | Uploads the source image and mask, applies the mask to the encoded latent, and samples only the masked region. A configured mask blur is applied before upload. |
| Zero-denoise source | When an uploaded source image has denoising strength set to `0`, Mogri skips diffusion and saves the source image directly. This also applies to an inpainting request; the mask is not used because no diffusion pass occurs. |

Normal generation requests use ComfyUI's `SaveImage` node, so generated output continues through Mogri's existing progress, cancellation, and image-download flow.

## Model Profiles

The model family selected in Mogri determines which ComfyUI loaders are used. The actual model and auxiliary resource names are discovered from the connected server.

| Model family | ComfyUI behavior |
| --- | --- |
| Stable Diffusion 1.5 and SDXL | Uses `CheckpointLoaderSimple` and the checkpoint's model, CLIP, and VAE outputs. |
| Z-Image Turbo | Uses `UNETLoader` when the model is exposed as a standalone diffusion model, a `CLIPLoader` with the `lumina2` type, and a compatible VAE such as `ae.safetensors`. Checkpoint-backed installations are also supported. |
| FLUX | Uses `UNETLoader` when appropriate, `DualCLIPLoader` with the `flux` type, a T5 text encoder, a CLIP-L text encoder, and a compatible VAE such as `ae.safetensors`. Checkpoint-backed installations are also supported. |
| Krea 2 Turbo and Krea 2 Raw | Supports checkpoint-backed models and standalone diffusion models. Uses a `CLIPLoader` with the `krea2` type, a compatible Qwen3-VL text encoder, and `qwen_image_vae.safetensors`. |

Model files are not included with Mogri. The selected model must be visible to the ComfyUI loader used by the generated workflow. If a model can be loaded either as a checkpoint or a standalone diffusion model, Mogri follows the resource shape reported by ComfyUI.

## Resource Discovery

When Mogri refreshes ComfyUI resources, it reads `/api/object_info` and populates the corresponding settings pickers from the node definitions exposed by the server.

| ComfyUI node | Resources discovered |
| --- | --- |
| `CheckpointLoaderSimple` | Checkpoint models |
| `UNETLoader` | Standalone diffusion models |
| `KSampler` | Samplers and schedulers |
| `VAELoader` | VAEs |
| `CLIPLoader` | Text encoders |
| `LoraLoader` | LoRAs |
| `UpscaleModelLoader` | Native image upscaler models |

Mogri matches persisted resource selections case-insensitively where possible and normalizes them to the exact name returned by ComfyUI before building a workflow. Refresh resources after changing ComfyUI's model paths or installing new resources.

### LoRAs

Selected LoRAs are inserted into the generated workflow in the order configured in Mogri. Standard checkpoint workflows use `LoraLoader`. Model families with dedicated text encoder loaders use `LoraLoaderModelOnly` so the LoRA is applied to the model without trying to alter the separately loaded text encoder.

## Native Upscaling

ComfyUI upscaling uses the selected model's native scale. Mogri discovers upscaler models from the `UpscaleModelLoader` entry in `/api/object_info`. Install or configure models in ComfyUI's `upscale_models` resource location, then refresh backend resources in Mogri. The upscaler picker contains the names ComfyUI exposes to `UpscaleModelLoader`.

For normal text-to-image, image-to-image, and inpainting requests, Mogri adds a post-generation chain equivalent to:

```text
VAEDecode -> ImageUpscaleWithModel -> SaveImage
             ^
             UpscaleModelLoader
```

For a zero-denoise source request, the source path is equivalent to:

```text
LoadImage -> ImageUpscaleWithModel -> SaveImage
             ^
             UpscaleModelLoader
```

Mogri therefore hides the generic upscale level control for ComfyUI. Forge's Hires Fix steps do not apply, and `UpscaleLevel` and `UpscaleSteps` are not used to construct a ComfyUI workflow. The selected upscaler model determines the output scale.

If the upscaler picker is empty:

1. Refresh backend resources in Mogri.
2. Confirm that the model is installed in a ComfyUI `upscale_models` location.
3. Confirm that `UpscaleModelLoader` lists the model in `/api/object_info`.
4. Verify the server URL and the ComfyUI version/resource configuration.

Very large native-scale outputs can require substantial server memory. Mogri does not currently add an in-app warning or size limit.

## Request Lifecycle

For a local ComfyUI request, Mogri:

1. Reads resource definitions from `/api/object_info`.
2. Uploads source images and masks through `/api/upload/image` when needed.
3. Submits the generated workflow to `/api/prompt`.
4. Listens for progress and completion through ComfyUI's WebSocket endpoint.
5. Downloads `SaveImage` output through `/api/view` and returns it to the app.

Cancellation is sent through ComfyUI's interrupt endpoint. The workflow still ends in `SaveImage`, including when native upscaling is enabled, so upscaled results use the same output-download path as other generations.

## Comfy Cloud

Comfy Cloud uses the same `ComfyUiService` workflow construction, resource handling, submission, progress, and output-download behavior as local ComfyUI, with the Comfy Cloud URL and API key supplied by the app.

Comfy Cloud resource configuration and authenticated upscaler behavior require separate end-to-end verification. This documentation does not claim that every Cloud resource configuration has been tested.

## Limitations

- ComfyUI does not expose Forge's Hires Fix workflow through Mogri. Native `ImageUpscaleWithModel` upscaling is the supported ComfyUI alternative.
- ComfyUI does not use Mogri's generic configurable 2x/4x upscale level. The selected upscaler's native scale is authoritative.
- Seamless or tiled generation is not currently advertised as a ComfyUI capability.
- Mogri relies on the standard node definitions listed above and does not configure arbitrary custom-node workflows.
- Older ComfyUI releases are not a primary support target. Mogri expects the current object-info and API shapes used by the supported node definitions.

The automated tests cover object-info parsing and workflow construction. They do not replace end-to-end validation against every ComfyUI release, model installation, or Comfy Cloud resource configuration.