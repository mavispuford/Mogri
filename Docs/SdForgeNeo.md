# SD Forge Neo

Mogri supports SD Forge Neo through its Automatic1111-compatible REST API. The backend uses the standard `/sdapi/v1` endpoints for model settings, generation, progress, resource discovery, upscaling, and cancellation.

This page describes the SD Forge Neo-specific setup, supported features, and known limitations. General app setup is covered in the [main README](../README.md).

## Quick Setup

1. Install SD Forge Neo and the model resources required by the model family you want to use.
2. Start the server with `--listen` so the phone can reach it over the local network. Use `--port` if you need a port other than the default `7860`.
3. On Windows, these arguments can be added to the `set COMMANDLINE_ARGS=` line in `webui-user.bat`.
4. In Mogri, open Settings, select **SD Forge Neo**, and enter the server URL, for example `http://192.168.1.x:7860`.
5. Use Mogri's resource refresh action after installing or changing models, LoRAs, VAEs, text encoders, or upscalers.

Mogri can send an optional custom authentication header with its requests. Make sure the server firewall allows connections from the phone and that both devices are on a network that permits local traffic.

## Supported Workflows

Mogri uses the following Forge API workflows:

| Workflow | Forge API | Behavior |
| --- | --- | --- |
| Text to image | `/sdapi/v1/txt2img` | Generates new images from positive and negative prompts, prompt styles, the selected model, sampling settings, dimensions, batch settings, and seed. Optional Forge Hires Fix can run as a second pass. |
| Image to image | `/sdapi/v1/img2img` | Uploads the source image and uses denoising strength to control how much of the source is preserved. |
| Inpainting | `/sdapi/v1/img2img` | Uses the source image plus a mask. Mogri uploads the mask and sends the configured mask blur value to Forge. |

An image included in a request selects the img2img endpoint. Inpainting is the same endpoint with a mask; Forge does not receive a separate inpainting request from Mogri.

Unlike the ComfyUI backend, Forge requests with denoising strength set to `0` still go through `/sdapi/v1/img2img`; Mogri does not replace that request with a local direct-source shortcut.

## Generation Settings

Mogri maps the main generation settings to Forge's processing request models:

| Mogri setting | Forge behavior |
| --- | --- |
| Prompt and negative prompt | Sent as `prompt` and `negative_prompt`, with selected prompt styles combined first. |
| Model | Selects the server checkpoint through the model/resource settings. |
| Width and height | Sent as the requested output dimensions. |
| Batch count and batch size | Sent as sequential iterations and images per batch. |
| Steps and guidance scale | Sent as `steps` and `cfg_scale`. |
| Sampler and scheduler | Sent as `sampler_name` and `scheduler`. Forge exposes both lists when supported by the server. |
| Seed | Sent as `seed`; `-1` asks Forge to choose a random seed. Returned seeds are read from the response metadata. |
| Tiling | Sent as Forge's `tiling` option. |
| Denoising strength | Used for img2img and inpainting. Lower values generally preserve more of the source image. |
| Mask blur | Sent with an inpainting request as `mask_blur`. |

## Model Profiles

Mogri provides profiles for these model families. The exact checkpoint, VAE, and text encoder names depend on what the connected Forge server exposes.

| Model family | Forge-specific resources and settings |
| --- | --- |
| Stable Diffusion 1.5 | Uses the selected checkpoint. No auxiliary VAE or text encoder is selected by default. |
| Stable Diffusion XL | Uses the selected checkpoint. No auxiliary VAE or text encoder is selected by default. |
| Z-Image Turbo | Uses a compatible `ae.safetensors` VAE and Qwen3 text encoder when available. Mogri also sends the distilled CFG setting supported by the profile. |
| FLUX | Uses a compatible `ae.safetensors` VAE, T5 text encoder, and CLIP-L text encoder when available. Mogri also sends the distilled CFG setting supported by the profile. |
| Krea 2 Turbo | Uses a compatible Krea checkpoint, Qwen3-VL text encoder, and the profile's Krea VAE. Mogri sends the profile's distilled CFG setting. |
| Krea 2 Raw | Uses a compatible Krea checkpoint, Qwen3-VL text encoder, and the profile's Krea VAE. This profile does not use a distilled CFG setting. |

Resource names can include Forge-specific suffixes. Mogri uses the names returned by the server rather than assuming that every installation uses identical filenames.

## Resource Discovery

Refreshing SD Forge Neo resources queries the following endpoints:

| Endpoint | Resources or state |
| --- | --- |
| `/sdapi/v1/options` | Current checkpoint and Forge options. This endpoint is required during initialization and is also used when saving model-related settings. |
| `/sdapi/v1/sd-models` | Checkpoints available to Forge. |
| `/sdapi/v1/samplers` | Sampler names and aliases. |
| `/sdapi/v1/schedulers` | Scheduler names, when the server exposes the endpoint. |
| `/sdapi/v1/loras` | LoRA names and aliases, when the server exposes the endpoint. |
| `/sdapi/v1/upscalers` | Upscaler names, model names, and reported scales, when the server exposes the endpoint. |
| `/sdapi/v1/sd-modules` | Auxiliary modules. Mogri exposes modules identified as VAEs and text encoders in the corresponding pickers. |

Some resource endpoints are optional for compatibility with Forge installations that do not expose them. If a picker is empty, refresh resources and confirm that the server version or extension set provides the corresponding endpoint.

When saving settings, Mogri can update Forge's active checkpoint, VAE selection, additional modules, and the UNet storage setting used by some newer model families. The server remains the source of truth for the final resource names.

## Hires Fix and Upscaling

Forge's upscaling behavior differs by workflow.

### Text to Image

When upscaling is enabled and all required Hires Fix values are present, Mogri sends Hires Fix fields directly in the `/sdapi/v1/txt2img` request:

```text
enable_hr       = true
hr_scale        = UpscaleLevel
hr_second_pass_steps = UpscaleSteps
hr_upscaler     = Upscaler
```

The Hires Fix pass runs inside Forge after the first diffusion pass. If a compatible VAE or text encoder is selected, Mogri can include those as Forge's additional Hires Fix modules. Hires Fix steps are a Forge-specific control and are not used by ComfyUI.

### Image to Image and Inpainting

For img2img and inpainting, Mogri first completes the `/sdapi/v1/img2img` request. When upscaling is enabled with a selected upscaler and a positive upscale level, it sends each returned image through `/sdapi/v1/extra-single-image`:

```text
Image from img2img/inpainting
    -> /sdapi/v1/extra-single-image
    -> upscaled image
```

The selected upscaler and `UpscaleLevel` control this post-processing request. `UpscaleSteps` is used for text-to-image Hires Fix, not for this img2img/inpainting post-upscale request.

If the upscaler or required scale/step values are missing, Forge Hires Fix is not enabled for text-to-image. Very large upscale levels can require substantial server memory and processing time.

## LoRAs and Prompt Styles

Selected LoRAs are appended to the prompt using Forge's extra-network syntax:

```text
<lora:loraname:strength>
```

Multiple LoRAs are appended in the order configured in Mogri. Prompt styles are combined with the positive and negative prompts before the request is sent.

## Request Lifecycle

For a generation request, Mogri:

1. Builds a txt2img or img2img request from the current settings.
2. Posts it to `/sdapi/v1/txt2img` or `/sdapi/v1/img2img`.
3. Polls `/sdapi/v1/progress` while the server is processing the request.
4. Returns progress updates and the final base64-encoded images to the app.
5. Applies the img2img/inpainting post-upscale request when configured.

Progress polling is performed through a separate API client so it can continue while the generation request is running. Cancellation is sent to `/sdapi/v1/interrupt`.

## PNG Metadata

Mogri can read generation settings from imported Forge images. It prefers the JSON metadata format and falls back to Forge's text-based parameters when necessary. Imported model names are matched against the refreshed Forge model list when possible, so sending a generated image back to Generate or Canvas can restore more of its original settings.

## Troubleshooting

### Mogri cannot connect

- Confirm SD Forge Neo is running with `--listen`.
- Confirm the phone can reach the computer's LAN address and port.
- Check the server firewall and any router isolation settings.
- Verify that the URL in Mogri uses the correct port, commonly `7860`.
- If the server requires authentication, configure the matching custom header in Mogri.

### Models or auxiliary resources are missing

1. Refresh backend resources in Mogri.
2. Confirm the resource appears in the corresponding Forge API response.
3. Check that the resource path and model family are compatible.
4. For newer model families, verify the required VAE and text encoder names exposed by `/sdapi/v1/sd-modules`.

### Hires Fix is not running

For text-to-image, confirm that upscaling is enabled, an upscaler is selected, `UpscaleLevel` is greater than zero, and `UpscaleSteps` is greater than zero. For img2img and inpainting, confirm that the upscaler is selected and the upscale level is positive; those workflows use the separate `extra-single-image` request instead.

## Limitations

- SD Forge Neo must expose the standard API routes used above. Mogri does not configure arbitrary Forge extensions or custom scripts.
- Forge Hires Fix is constructed for text-to-image. Image-to-image and inpainting use the separate post-upscale endpoint instead.
- Optional resource endpoints may be unavailable on some Forge versions or installations, leaving the corresponding picker empty.
- Upscale levels and Hires Fix steps can increase memory use and processing time; Mogri does not currently add a memory warning or output-size guard.
- SD Forge Neo is a local/server backend in Mogri. Comfy Cloud is documented separately in [ComfyUI.md](ComfyUI.md).

The automated tests cover Forge metadata parsing and the shared settings behavior. They do not replace end-to-end validation against every SD Forge Neo version, extension set, model installation, or authentication configuration.