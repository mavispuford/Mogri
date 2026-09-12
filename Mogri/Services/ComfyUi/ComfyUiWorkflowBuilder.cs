using Mogri.Models;
using Mogri.Helpers;
using Mogri.Enums;

namespace Mogri.Services.ComfyUi;

public static class ComfyUiWorkflowBuilder
{
    private static readonly Random Random = new();

    public static (Dictionary<string, object> Workflow, long Seed) BuildTextToImageWorkflow(
        PromptSettings settings,
        IReadOnlyCollection<string>? diffusionModels = null)
    {
        return BuildWorkflowInternal(settings, "txt2img", diffusionModels: diffusionModels);
    }

    public static (Dictionary<string, object> Workflow, long Seed) BuildImageToImageWorkflow(
        PromptSettings settings,
        string uploadedImageFilename,
        IReadOnlyCollection<string>? diffusionModels = null)
    {
        return BuildWorkflowInternal(settings, "img2img", uploadedImageFilename, diffusionModels: diffusionModels);
    }

    public static (Dictionary<string, object> Workflow, long Seed) BuildInpaintingWorkflow(
        PromptSettings settings,
        string uploadedImageFilename,
        string uploadedMaskFilename,
        IReadOnlyCollection<string>? diffusionModels = null)
    {
        return BuildWorkflowInternal(settings, "inpaint", uploadedImageFilename, uploadedMaskFilename, diffusionModels);
    }
    
    private static (Dictionary<string, object> Workflow, long Seed) BuildWorkflowInternal(
        PromptSettings settings,
        string mode,
        string? imageFilename = null,
        string? maskFilename = null,
        IReadOnlyCollection<string>? diffusionModels = null)
    {
        if (IsDirectSourceWorkflow(settings, mode, imageFilename))
        {
            return BuildDirectSourceWorkflow(settings, imageFilename!);
        }

        var workflow = new Dictionary<string, object>();
        var nodeIdCounter = 1;
        var usesSeparateModel = UsesSeparateModel(settings, diffusionModels);
        var usesDedicatedTextEncoder = settings.ModelType is ModelType.ZImageTurbo or ModelType.Flux or ModelType.Krea2Turbo or ModelType.Krea2Raw;
        var usesExternalVae = usesDedicatedTextEncoder || HasConcreteResource(settings.Vae);
        var modelKey = settings.Model?.Key ?? "v1-5-pruned-emaonly.ckpt";

        if (usesDedicatedTextEncoder && string.IsNullOrWhiteSpace(settings.TextEncoder))
        {
            throw new InvalidOperationException($"{settings.ModelType} workflows require a text encoder.");
        }

        if (usesDedicatedTextEncoder && string.IsNullOrWhiteSpace(settings.Vae))
        {
            throw new InvalidOperationException($"{settings.ModelType} workflows require a VAE.");
        }

        if (settings.ModelType == ModelType.Flux && string.IsNullOrWhiteSpace(settings.TextEncoderSecondary))
        {
            throw new InvalidOperationException("Flux workflows require a second text encoder.");
        }

        object[] currentModelOutput;
        object[] currentClipOutput = new object[] { "1", 1 };
        object[] vaeOutput;
            string? checkpointNodeId = null;

        if (usesSeparateModel)
        {
            var modelNodeId = nodeIdCounter.ToString();
            AddNode(workflow, modelNodeId, "UNETLoader", new Dictionary<string, object>
            {
                ["unet_name"] = modelKey,
                ["weight_dtype"] = "default"
            });

            nodeIdCounter++;
            currentModelOutput = new object[] { modelNodeId, 0 };
        }
        else
        {
            checkpointNodeId = nodeIdCounter.ToString();
            AddNode(workflow, checkpointNodeId, "CheckpointLoaderSimple", new Dictionary<string, object>
            {
                ["ckpt_name"] = modelKey
            });
            nodeIdCounter++;

            currentModelOutput = new object[] { checkpointNodeId, 0 };
            currentClipOutput = new object[] { checkpointNodeId, 1 };
        }

        if (usesDedicatedTextEncoder)
        {
            var clipNodeId = nodeIdCounter.ToString();
            if (settings.ModelType == ModelType.Flux)
            {
                AddNode(workflow, clipNodeId, "DualCLIPLoader", new Dictionary<string, object>
                {
                    ["clip_name1"] = settings.TextEncoderSecondary!,
                    ["clip_name2"] = settings.TextEncoder!,
                    ["type"] = "flux"
                });
            }
            else
            {
                AddNode(workflow, clipNodeId, "CLIPLoader", new Dictionary<string, object>
                {
                    ["clip_name"] = settings.TextEncoder!,
                    ["type"] = GetClipLoaderType(settings.ModelType)
                });
            }

            nodeIdCounter++;
            currentClipOutput = new object[] { clipNodeId, 0 };
        }

        if (usesExternalVae)
        {
            var vaeNodeId = nodeIdCounter.ToString();
            AddNode(workflow, vaeNodeId, "VAELoader", new Dictionary<string, object>
            {
                ["vae_name"] = settings.Vae!
            });
            nodeIdCounter++;
            vaeOutput = new object[] { vaeNodeId, 0 };
        }
        else
        {
                vaeOutput = new object[] { checkpointNodeId!, 2 };
        }

        if (settings.Loras != null)
        {
            foreach (var lora in settings.Loras)
            {
                var loraNodeId = nodeIdCounter.ToString();
                var modelInput = new object[] { currentModelOutput[0], currentModelOutput[1] };

                var loraInputs = new Dictionary<string, object>
                {
                    ["lora_name"] = lora.Name,
                    ["strength_model"] = lora.Strength,
                    ["model"] = modelInput,
                };

                if (usesDedicatedTextEncoder)
                {
                    AddNode(workflow, loraNodeId, "LoraLoaderModelOnly", loraInputs);
                }
                else
                {
                    loraInputs["strength_clip"] = lora.Strength;
                    loraInputs["clip"] = new object[] { currentClipOutput[0], currentClipOutput[1] };
                    AddNode(workflow, loraNodeId, "LoraLoader", loraInputs);
                }
                
                currentModelOutput = new object[] { loraNodeId, 0 };
                nodeIdCounter++;
            }
        }

        // 2. Prompts
        var positivePromptNodeId = nodeIdCounter.ToString();
        var (positivePrompt, _) = settings.GetCombinedPromptAndPromptStyles();
        
        AddNode(workflow, positivePromptNodeId, "CLIPTextEncode", new Dictionary<string, object>
        {
            ["text"] = positivePrompt ?? string.Empty,
            ["clip"] = new object[] { currentClipOutput[0], currentClipOutput[1] }
        });
        nodeIdCounter++;

        var negativePromptNodeId = nodeIdCounter.ToString();
        var (_, negativePrompt) = settings.GetCombinedPromptAndPromptStyles();
        
        AddNode(workflow, negativePromptNodeId, "CLIPTextEncode", new Dictionary<string, object>
        {
            ["text"] = negativePrompt ?? string.Empty,
            ["clip"] = new object[] { currentClipOutput[0], currentClipOutput[1] }
        });
        nodeIdCounter++;

        // Latent Source
        string latentNodeId;
        
        if (mode == "txt2img")
        {
             latentNodeId = nodeIdCounter.ToString();
             AddNode(workflow, latentNodeId, "EmptyLatentImage", new Dictionary<string, object>
             {
                 ["width"] = (int)settings.Width, 
                 ["height"] = (int)settings.Height,
                 ["batch_size"] = settings.BatchSize
             });
             nodeIdCounter++;
        }
        else
        {
            // Load Image
            var loadImageNodeId = nodeIdCounter.ToString();
            AddNode(workflow, loadImageNodeId, "LoadImage", new Dictionary<string, object>
            {
                ["image"] = imageFilename!
            });
            nodeIdCounter++;
            
            if (mode == "inpaint")
            {
                 var loadMaskNodeId = nodeIdCounter.ToString();
                 AddNode(workflow, loadMaskNodeId, "LoadImage", new Dictionary<string, object>
                 {
                     ["image"] = maskFilename!
                 });
                 nodeIdCounter++;

                 var invertMaskNodeId = nodeIdCounter.ToString();
                 AddNode(workflow, invertMaskNodeId, "InvertMask", new Dictionary<string, object>
                 {
                     ["mask"] = new object[] { loadMaskNodeId, 1 }
                 });
                 nodeIdCounter++;

                 var encodedLatentNodeId = nodeIdCounter.ToString();
                 
                 AddNode(workflow, encodedLatentNodeId, "VAEEncode", new Dictionary<string, object>
                 {
                     ["pixels"] = new object[] { loadImageNodeId, 0 },
                     ["vae"] = new object[] { vaeOutput[0], vaeOutput[1] }
                 });
                 nodeIdCounter++;

                 latentNodeId = nodeIdCounter.ToString();
                 AddNode(workflow, latentNodeId, "SetLatentNoiseMask", new Dictionary<string, object>
                 {
                     ["samples"] = new object[] { encodedLatentNodeId, 0 },
                     ["mask"] = new object[] { invertMaskNodeId, 0 }
                 });
            }
            else
            {
                latentNodeId = nodeIdCounter.ToString();

                AddNode(workflow, latentNodeId, "VAEEncode", new Dictionary<string, object>
                {
                    ["pixels"] = new object[] { loadImageNodeId, 0 },
                    ["vae"] = new object[] { vaeOutput[0], vaeOutput[1] }
                });
            }
            nodeIdCounter++;
        }

        // KSampler
        var kSamplerNodeId = nodeIdCounter.ToString();
        long seed = settings.Seed == -1 ? Random.NextInt64(0, long.MaxValue) : (long)settings.Seed;
        var denoise = mode == "txt2img" ? 1.0f : Math.Min(Math.Max((float)settings.DenoisingStrength, 0.01f), 1.0f);
        
        AddNode(workflow, kSamplerNodeId, "KSampler", new Dictionary<string, object>
        {
            ["seed"] = seed,
            ["steps"] = settings.Steps,
            ["cfg"] = settings.GuidanceScale,
            ["sampler_name"] = settings.Sampler ?? "euler", 
            ["scheduler"] = settings.Scheduler ?? "normal",
            ["denoise"] = denoise,
            ["model"] = new object[] { currentModelOutput[0], currentModelOutput[1] },
            ["positive"] = new object[] { positivePromptNodeId, 0 },
            ["negative"] = new object[] { negativePromptNodeId, 0 },
            ["latent_image"] = new object[] { latentNodeId, 0 }
        });
        nodeIdCounter++;

        // VAE Decode
        var vaeDecodeNodeId = nodeIdCounter.ToString();
        AddNode(workflow, vaeDecodeNodeId, "VAEDecode", new Dictionary<string, object>
        {
            ["samples"] = new object[] { kSamplerNodeId, 0 },
            ["vae"] = new object[] { vaeOutput[0], vaeOutput[1] }
        });
        nodeIdCounter++;

        var outputNodeId = vaeDecodeNodeId;
        if (IsComfyUiUpscalingEnabled(settings))
        {
            outputNodeId = AddUpscalerNodes(workflow, ref nodeIdCounter, outputNodeId, settings.Upscaler!);
        }

        AddSaveImageNode(workflow, ref nodeIdCounter, outputNodeId);

        return (workflow, seed);
    }

    private static (Dictionary<string, object> Workflow, long Seed) BuildDirectSourceWorkflow(
        PromptSettings settings,
        string imageFilename)
    {
        var workflow = new Dictionary<string, object>();
        var nodeIdCounter = 1;
        var loadImageNodeId = nodeIdCounter.ToString();

        AddNode(workflow, loadImageNodeId, "LoadImage", new Dictionary<string, object>
        {
            ["image"] = imageFilename
        });
        nodeIdCounter++;

        var outputNodeId = loadImageNodeId;
        if (IsComfyUiUpscalingEnabled(settings))
        {
            outputNodeId = AddUpscalerNodes(workflow, ref nodeIdCounter, outputNodeId, settings.Upscaler!);
        }

        AddSaveImageNode(workflow, ref nodeIdCounter, outputNodeId);

        return (workflow, -1);
    }

    private static string AddUpscalerNodes(
        Dictionary<string, object> workflow,
        ref int nodeIdCounter,
        string imageNodeId,
        string upscalerName)
    {
        var upscalerLoaderNodeId = nodeIdCounter.ToString();
        AddNode(workflow, upscalerLoaderNodeId, "UpscaleModelLoader", new Dictionary<string, object>
        {
            ["model_name"] = upscalerName
        });
        nodeIdCounter++;

        var imageUpscaleNodeId = nodeIdCounter.ToString();
        AddNode(workflow, imageUpscaleNodeId, "ImageUpscaleWithModel", new Dictionary<string, object>
        {
            ["upscale_model"] = new object[] { upscalerLoaderNodeId, 0 },
            ["image"] = new object[] { imageNodeId, 0 }
        });
        nodeIdCounter++;

        return imageUpscaleNodeId;
    }

    private static void AddSaveImageNode(
        Dictionary<string, object> workflow,
        ref int nodeIdCounter,
        string imageNodeId)
    {
        var saveImageNodeId = nodeIdCounter.ToString();
        AddNode(workflow, saveImageNodeId, "SaveImage", new Dictionary<string, object>
        {
            ["filename_prefix"] = "Mogri",
            ["images"] = new object[] { imageNodeId, 0 }
        });
    }

    private static bool IsDirectSourceWorkflow(PromptSettings settings, string mode, string? imageFilename)
    {
        return mode is "img2img" or "inpaint" &&
               !string.IsNullOrWhiteSpace(imageFilename) &&
               settings.DenoisingStrength <= 0;
    }

    private static bool IsComfyUiUpscalingEnabled(PromptSettings settings)
    {
        return settings.EnableUpscaling && !string.IsNullOrWhiteSpace(settings.Upscaler);
    }

    private static bool IsStandaloneKreaModel(string modelKey)
    {
        return modelKey.Contains("krea2_turbo_", StringComparison.OrdinalIgnoreCase) ||
               modelKey.Contains("krea2turbo_", StringComparison.OrdinalIgnoreCase) ||
               modelKey.Contains("krea2_raw_", StringComparison.OrdinalIgnoreCase) ||
               modelKey.Contains("krea2raw_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsesSeparateModel(PromptSettings settings, IReadOnlyCollection<string>? diffusionModels)
    {
        var supportsSeparateModel = settings.ModelType is ModelType.ZImageTurbo or ModelType.Flux or
            ModelType.Krea2Turbo or ModelType.Krea2Raw;
        if (!supportsSeparateModel)
        {
            return false;
        }

        var modelKey = settings.Model?.Key ?? string.Empty;
        if (diffusionModels != null)
        {
            return diffusionModels.Any(model =>
                string.Equals(model, modelKey, StringComparison.OrdinalIgnoreCase));
        }

        return settings.ModelType is ModelType.ZImageTurbo or ModelType.Flux ||
               (settings.ModelType is ModelType.Krea2Turbo or ModelType.Krea2Raw &&
                IsStandaloneKreaModel(modelKey));
    }

    private static string GetClipLoaderType(ModelType modelType)
    {
        return modelType switch
        {
            ModelType.ZImageTurbo => "lumina2",
            ModelType.Krea2Turbo or ModelType.Krea2Raw => "krea2",
            _ => throw new ArgumentOutOfRangeException(nameof(modelType), modelType, "The model type does not use a dedicated CLIP loader.")
        };
    }

    private static bool HasConcreteResource(string? resource)
    {
        return !string.IsNullOrWhiteSpace(resource) &&
               !string.Equals(resource, "Automatic", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(resource, "None", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddNode(Dictionary<string, object> workflow, string id, string classType, Dictionary<string, object> inputs)
    {
        workflow[id] = new Dictionary<string, object>
        {
            ["class_type"] = classType,
            ["inputs"] = inputs
        };
    }
}
