# Mobile Diffusion

Mobile Diffusion is a .NET MAUI mobile application for image generation and editing. It combines a finger-friendly mobile UI with a mix of on-device processing and a remote server for heavy-duty generation tasks. While it is not a replacement for professional desktop image editors, Mobile Diffusion aims to bridge the gap for mobile workflows.

## Overview

There are two main tabs in the app:

- **Generate**: The user's main entry point to view generation results and edit settings. Image generation can continue in the background via a persistent notification (Android only).
- **Canvas**: A workspace to mask/paint on generated images (or images from the file system) or draw freehand sketches to use as a base for generation.

### Example Workflow

1.  Generate one or more "photo of a cat" images.
2.  Send a selected image to the **Canvas**.
3.  Mask the area above the cat and send the updated canvas image back to the **Generate** tab.
4.  Generate with a new prompt (e.g., "a top hat"), resulting in a cat wearing a top hat etc.

## Architecture

The application follows the MVVM pattern and utilizes standard .NET MAUI features along with the CommunityToolkit.

For a detailed breakdown of the application structure, including Views, ViewModels, and Services, please see [Architecture.md](docs/Architecture.md).

## Getting Started

### Prerequisites

- .NET 10.0 SDK (or later) for Android/iOS workloads.
- A running instance of **Stable Diffusion WebUI Forge** (or compatible Neo-supported backend).

### Configuration

1.  Build and deploy the application to your device.
2.  Navigate to the **Settings** page.
3.  Select your backend (**SD Forge Neo** or **ComfyUI**) from the dropdown.
4.  Enter your backend server URL (e.g., `http://192.168.1.x:7860` for Forge, `http://192.168.1.x:8188` for ComfyUI).
5.  If using Comfy Cloud, enter your API Key.