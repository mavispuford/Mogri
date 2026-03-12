using Mogri.Models;
using Mogri.Helpers;

namespace Mogri.Services.ComfyUi;

public static class ComfyUiWorkflowBuilder
{
    private static readonly Random Random = new();

    public static (Dictionary<string, object> Workflow, long Seed) BuildTextToImageWorkflow(PromptSettings settings)
    {
        return BuildWorkflowInternal(settings, "txt2img");
    }

    public static (Dictionary<string, object> Workflow, long Seed) BuildImageToImageWorkflow(PromptSettings settings, string uploadedImageFilename)
    {
        return BuildWorkflowInternal(settings, "img2img", uploadedImageFilename);
    }

    public static (Dictionary<string, object> Workflow, long Seed) BuildInpaintingWorkflow(PromptSettings settings, string uploadedImageFilename, string uploadedMaskFilename)
    {
        return BuildWorkflowInternal(settings, "inpaint", uploadedImageFilename, uploadedMaskFilename);
    }
    
    private static (Dictionary<string, object> Workflow, long Seed) BuildWorkflowInternal(PromptSettings settings, string mode, string? imageFilename = null, string? maskFilename = null)
    {
        var workflow = new Dictionary<string, object>();
        var nodeIdCounter = 1;

        // 1. Load Checkpoint
        var checkpointNodeId = nodeIdCounter.ToString();
        AddNode(workflow, checkpointNodeId, "CheckpointLoaderSimple", new Dictionary<string, object>
        {
            ["ckpt_name"] = settings.Model?.Key ?? "v1-5-pruned-emaonly.ckpt"
        });
        nodeIdCounter++;

        // Handle LoRAs
        var currentModelOutput = new object[] { checkpointNodeId, 0 };
        var currentClipOutput = new object[] { checkpointNodeId, 1 };
        var vaeOutput = new object[] { checkpointNodeId, 2 };

        if (settings.Loras != null)
        {
            foreach (var lora in settings.Loras)
            {
                var loraNodeId = nodeIdCounter.ToString();
                // Clone inputs to avoid reference issues if reused (though arrays are ref types, creating new array is safer)
                var modelInput = new object[] { currentModelOutput[0], currentModelOutput[1] };
                var clipInput = new object[] { currentClipOutput[0], currentClipOutput[1] };

                AddNode(workflow, loraNodeId, "LoraLoader", new Dictionary<string, object>
                {
                    ["lora_name"] = lora.Name,
                    ["strength_model"] = lora.Strength,
                    ["strength_clip"] = lora.Strength,
                    ["model"] = modelInput,
                    ["clip"] = clipInput
                });
                
                currentModelOutput = new object[] { loraNodeId, 0 };
                currentClipOutput = new object[] { loraNodeId, 1 };
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
            
            latentNodeId = nodeIdCounter.ToString();
            
            if (mode == "inpaint")
            {
                 // Load Mask (if separate file)
                 var loadMaskNodeId = nodeIdCounter.ToString();
                 AddNode(workflow, loadMaskNodeId, "LoadImage", new Dictionary<string, object>
                 {
                     ["image"] = maskFilename!
                 });
                 nodeIdCounter++;
                 
                 AddNode(workflow, latentNodeId, "VAEEncodeForInpaint", new Dictionary<string, object>
                 {
                     ["pixels"] = new object[] { loadImageNodeId, 0 },
                     ["vae"] = new object[] { vaeOutput[0], vaeOutput[1] },
                     ["mask"] = new object[] { loadMaskNodeId, 1 }, // output 1 is mask
                     ["grow_mask_by"] = settings.MaskBlur
                 });
            }
            else // img2img
            {
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

        // Save Image
        var saveImageNodeId = nodeIdCounter.ToString();
        AddNode(workflow, saveImageNodeId, "SaveImage", new Dictionary<string, object>
        {
            ["filename_prefix"] = "Mogri",
            ["images"] = new object[] { vaeDecodeNodeId, 0 }
        });

        return (workflow, seed);
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
