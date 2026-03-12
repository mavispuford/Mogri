using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Models;

public class PromptSettings
{
    /// <summary>
    /// Whether to apply an upscaler to the generated image.
    /// </summary>
    public bool EnableUpscaling { get; set; } = false;

    /// <summary>
    /// Whether to resize/crop the input image to fit dimensions on the server before processing.
    /// </summary>
    public bool EnableFitServerSide { get; set; } = true;

    /// <summary>
    /// Whether to resize/crop the input image on the client before sending.
    /// </summary>
    public bool FitClientSide { get; set; } = true;

    /// <summary>
    /// Classifier-Free Guidance scale. Controls how closely the generation follows the prompt.
    /// </summary>
    public double GuidanceScale { get; set; } = 7.5;

    /// <summary>
    /// Specific guidance scale for distilled models (e.g. Flux.1).
    /// These models behave differently than standard SD and typically require lower values (e.g. 2.0 - 4.0).
    /// Increasing the value (e.g., to 4 or 5) improves prompt adherence but may introduce overcooked, high-contrast, or distorted results, particularly in Flux.1-dev.
    /// Setting the value too low (e.g., &lt; 1.0) can lead to images that do not follow the prompt or lack detail.
    /// </summary>
    public double? DistilledCfgScale { get; set; } = 3;

    /// <summary>
    /// Target image height in pixels.
    /// </summary>
    public double Height { get; set; } = 1024;

    /// <summary>
    /// Base64 encoded string of the initial image for Img2Img operations.
    /// </summary>
    public string? InitImage { get; set; }

    /// <summary>
    /// Base64 encoded thumbnail of the initial image for UI display.
    /// </summary>
    public string? InitImageThumbnail { get; set; }

    /// <summary>
    /// Whether to invert the mask (paint outside vs inside).
    /// </summary>
    public OnOff InvertMask { get; set; }

    /// <summary>
    /// Base64 encoded string of the mask image for inpainting.
    /// </summary>
    public string? Mask { get; set; }

    /// <summary>
    /// Amount of blur to apply to the mask edges in pixels.
    /// </summary>
    public int MaskBlur { get; set; } = 5;

    /// <summary>
    /// The high-level model type (SD, SDXL, Flux) which may affect parameter defaults.
    /// </summary>
    public ModelType ModelType { get; set; } = ModelType.SDXL;

    /// <summary>
    /// Number of sampling steps.
    /// </summary>
    public int Steps { get; set; } = 30;

    /// <summary>
    /// Number of batches of images to generate (sequential).
    /// </summary>
    public int BatchCount { get; set; } = 1;

    /// <summary>
    /// Number of images per batch (parallel).
    /// </summary>
    public int BatchSize { get; set; } = 1;

    /// <summary>
    /// The positive prompt text describing what to generate.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// The negative prompt text describing what to avoid.
    /// </summary>
    public string NegativePrompt { get; set; } = string.Empty;

    /// <summary>
    /// Strength of the denoising for Img2Img (0.0 to 1.0). 
    /// Lower values preserve more of the original image.
    /// </summary>
    public double DenoisingStrength { get; set; } = .5;

    /// <summary>
    /// The selected model checkpoint to use.
    /// </summary>
    public IModelViewModel? Model { get; set; }

    /// <summary>
    /// The sampling algorithm to use (e.g., "Euler a", "DPM++ SDE").
    /// </summary>
    public string? Sampler { get; set; }

    /// <summary>
    /// The scheduler/noise schedule to use (e.g., "Karras", "Exponential").
    /// </summary>
    public string? Scheduler { get; set; }

    /// <summary>
    /// The VAE to use.
    /// </summary>
    public string? Vae { get; set; }

    /// <summary>
    /// The Text Encoder to use.
    /// </summary>
    public string? TextEncoder { get; set; }

    /// <summary>
    /// Generic intent for tiled/seamless generation.
    /// </summary>
    public bool EnableTiling { get; set; }

    /// <summary>
    /// The random seed. -1 indicates a random seed should be generated.
    /// </summary>
    public long Seed { get; set; } = -1;

    /// <summary>
    /// Name of the upscaler model to use.
    /// </summary>
    public string? Upscaler { get; set; }

    /// <summary>
    /// Multiplier for the upscaling (e.g., 2, 4).
    /// </summary>
    public int UpscaleLevel { get; set; } = 2;

    /// <summary>
    /// Number of steps specific to the High-Res fix pass.
    /// </summary>
    public int UpscaleSteps { get; set; } = 10;

    /// <summary>
    /// Strength of variation when using variation seeds.
    /// </summary>
    public double VariationAmount { get; set; } = .1;

    /// <summary>
    /// Target image width in pixels.
    /// </summary>
    public double Width { get; set; } = 1024;

    /// <summary>
    /// Whether to enable variation seeds.
    /// </summary>
    public OnOff WithVariations { get; set; }

    /// <summary>
    /// List of LoRA networks to apply.
    /// </summary>
    public List<ILoraViewModel> Loras { get; set; } = new();

    /// <summary>
    /// List of predefined prompt styles to apply.
    /// </summary>
    public List<IPromptStyleViewModel> PromptStyles { get; set; } = new();

    /// <summary>
    /// Stores backend-specific parameters that don't fit into the standard schema.
    /// Use this for backend-specific settings like ComfyUI workflow IDs, specific node overrides, or extension parameters.
    /// </summary>
    public Dictionary<string, object> BackendParameters { get; set; } = new();

    public PromptSettings Clone()
    {
        return new PromptSettings
        {
            EnableUpscaling = EnableUpscaling,
            EnableFitServerSide = EnableFitServerSide,
            FitClientSide = FitClientSide,
            GuidanceScale = GuidanceScale,
            DistilledCfgScale = DistilledCfgScale,
            Height = Height,
            InitImage = InitImage,
            InitImageThumbnail = InitImageThumbnail,
            InvertMask = InvertMask,
            Mask = Mask,
            MaskBlur = MaskBlur,
            ModelType = ModelType,
            Steps = Steps,
            BatchCount = BatchCount,
            BatchSize = BatchSize,
            Prompt = Prompt,
            NegativePrompt = NegativePrompt,
            DenoisingStrength = DenoisingStrength,
            Model = Model,
            Sampler = Sampler,
            Scheduler = Scheduler,
            Vae = Vae,
            TextEncoder = TextEncoder,
            EnableTiling = EnableTiling,
            Seed = Seed,
            Upscaler = Upscaler,
            UpscaleLevel = UpscaleLevel,
            UpscaleSteps = UpscaleSteps,
            VariationAmount = VariationAmount,
            Width = Width,
            WithVariations = WithVariations,
            Loras = new List<ILoraViewModel>(Loras),
            PromptStyles = new List<IPromptStyleViewModel>(PromptStyles),
            BackendParameters = BackendParameters.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value is ICloneable c ? c.Clone() : kvp.Value)
        };
    }
}
