using Moq;
using Mogri.Enums;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.Services.ComfyUi;
using Xunit;

namespace Mogri.Tests.Services;

public class ComfyUiWorkflowBuilderTests
{
    [Fact]
    public void BuildTextToImageWorkflow_KreaStandaloneModel_UsesSeparateLoaders()
    {
        // Arrange
        var settings = CreateKreaSettings("Krea2Turbo_quanto_bf16_int8.safetensors");
        settings.Loras.Add(CreateLora("krea2_style_reference.safetensors", 0.8));

        // Act
        var (workflow, _) = ComfyUiWorkflowBuilder.BuildTextToImageWorkflow(settings);

        // Assert
        Assert.Equal("UNETLoader", GetClassType(workflow, "1"));
        Assert.Equal("Krea2Turbo_quanto_bf16_int8.safetensors", GetInput(workflow, "1", "unet_name"));
        Assert.Equal("CLIPLoader", GetClassType(workflow, "2"));
        Assert.Equal("Qwen3-VL-4B.safetensors", GetInput(workflow, "2", "clip_name"));
        Assert.Equal("krea2", GetInput(workflow, "2", "type"));
        Assert.Equal("VAELoader", GetClassType(workflow, "3"));
        Assert.Equal("Qwen2D_VAE.safetensors", GetInput(workflow, "3", "vae_name"));
        Assert.Equal("LoraLoaderModelOnly", GetClassType(workflow, "4"));
        Assert.Equal(new object[] { "1", 0 }, GetInput(workflow, "4", "model"));
        Assert.Equal(new object[] { "2", 0 }, GetInput(workflow, "5", "clip"));
        Assert.Equal(new object[] { "3", 0 }, GetInput(workflow, "9", "vae"));
    }

    [Fact]
    public void BuildTextToImageWorkflow_KreaIntegratedModel_UsesExplicitClipAndVae()
    {
        // Arrange
        var settings = CreateKreaSettings("Krea2Turbo.safetensors");

        // Act
        var (workflow, _) = ComfyUiWorkflowBuilder.BuildTextToImageWorkflow(settings);

        // Assert
        Assert.Equal("CheckpointLoaderSimple", GetClassType(workflow, "1"));
        Assert.Equal("Krea2Turbo.safetensors", GetInput(workflow, "1", "ckpt_name"));
        Assert.Equal("CLIPLoader", GetClassType(workflow, "2"));
        Assert.Equal("VAELoader", GetClassType(workflow, "3"));
        Assert.Equal(new object[] { "2", 0 }, GetInput(workflow, "4", "clip"));
        Assert.Equal(new object[] { "3", 0 }, GetInput(workflow, "8", "vae"));
    }

    [Fact]
    public void BuildTextToImageWorkflow_StandardModel_UsesCheckpointOutputs()
    {
        // Arrange
        var settings = new PromptSettings
        {
            ModelType = ModelType.SDXL,
            Model = CreateModel("sd_xl_base_1.0.safetensors"),
            Prompt = "a mountain lake",
            NegativePrompt = "blurry"
        };

        // Act
        var (workflow, _) = ComfyUiWorkflowBuilder.BuildTextToImageWorkflow(settings);

        // Assert
        Assert.Equal("CheckpointLoaderSimple", GetClassType(workflow, "1"));
        Assert.Equal(new object[] { "1", 1 }, GetInput(workflow, "2", "clip"));
        Assert.Equal(new object[] { "1", 2 }, GetInput(workflow, "6", "vae"));
    }

    [Fact]
    public void BuildInpaintingWorkflow_PreservesSourceLatentAndAppliesInvertedNoiseMask()
    {
        // Arrange
        var settings = CreateKreaSettings("Krea2Turbo.safetensors");

        // Act
        var (workflow, _) = ComfyUiWorkflowBuilder.BuildInpaintingWorkflow(
            settings,
            "source.png",
            "mask.png");

        // Assert
        Assert.Equal("LoadImage", GetClassType(workflow, "6"));
        Assert.Equal("LoadImage", GetClassType(workflow, "7"));
        Assert.Equal("InvertMask", GetClassType(workflow, "8"));
        Assert.Equal(new object[] { "7", 1 }, GetInput(workflow, "8", "mask"));
        Assert.Equal("VAEEncode", GetClassType(workflow, "9"));
        Assert.Equal(new object[] { "6", 0 }, GetInput(workflow, "9", "pixels"));
        Assert.Equal("SetLatentNoiseMask", GetClassType(workflow, "10"));
        Assert.Equal(new object[] { "9", 0 }, GetInput(workflow, "10", "samples"));
        Assert.Equal(new object[] { "8", 0 }, GetInput(workflow, "10", "mask"));
    }

    [Fact]
    public void BuildTextToImageWorkflow_ZImageTurbo_UsesDedicatedLoaders()
    {
        // Arrange
        var settings = new PromptSettings
        {
            ModelType = ModelType.ZImageTurbo,
            Model = CreateModel("z_image_turbo_bf16.safetensors"),
            TextEncoder = "qwen_3_4b.safetensors",
            Vae = "ae.safetensors",
            Prompt = "a mountain lake"
        };

        // Act
        var (workflow, _) = ComfyUiWorkflowBuilder.BuildTextToImageWorkflow(settings);

        // Assert
        Assert.Equal("UNETLoader", GetClassType(workflow, "1"));
        Assert.Equal("CLIPLoader", GetClassType(workflow, "2"));
        Assert.Equal("lumina2", GetInput(workflow, "2", "type"));
        Assert.Equal("VAELoader", GetClassType(workflow, "3"));
    }

    [Fact]
    public void BuildTextToImageWorkflow_Flux_UsesDualClipLoader()
    {
        // Arrange
        var settings = new PromptSettings
        {
            ModelType = ModelType.Flux,
            Model = CreateModel("flux1-dev.safetensors"),
            TextEncoder = "t5xxl_fp16.safetensors",
            TextEncoderSecondary = "clip_l.safetensors",
            Vae = "ae.safetensors",
            Prompt = "a mountain lake"
        };

        // Act
        var (workflow, _) = ComfyUiWorkflowBuilder.BuildTextToImageWorkflow(settings);

        // Assert
        Assert.Equal("UNETLoader", GetClassType(workflow, "1"));
        Assert.Equal("DualCLIPLoader", GetClassType(workflow, "2"));
        Assert.Equal("clip_l.safetensors", GetInput(workflow, "2", "clip_name1"));
        Assert.Equal("t5xxl_fp16.safetensors", GetInput(workflow, "2", "clip_name2"));
        Assert.Equal("flux", GetInput(workflow, "2", "type"));
        Assert.Equal("VAELoader", GetClassType(workflow, "3"));
    }

    [Fact]
    public void BuildTextToImageWorkflow_ZImageCheckpointBacked_UsesCheckpointForModel()
    {
        // Arrange
        var settings = new PromptSettings
        {
            ModelType = ModelType.ZImageTurbo,
            Model = CreateModel("z_image_turbo_bf16.safetensors"),
            TextEncoder = "qwen_3_4b.safetensors",
            Vae = "ae.safetensors",
            Prompt = "a mountain lake"
        };

        // Act
        var (workflow, _) = ComfyUiWorkflowBuilder.BuildTextToImageWorkflow(
            settings,
            Array.Empty<string>());

        // Assert
        Assert.Equal("CheckpointLoaderSimple", GetClassType(workflow, "1"));
        Assert.Equal("CLIPLoader", GetClassType(workflow, "2"));
        Assert.Equal("VAELoader", GetClassType(workflow, "3"));
    }

    private static PromptSettings CreateKreaSettings(string modelKey)
    {
        return new PromptSettings
        {
            ModelType = ModelType.Krea2Turbo,
            Model = CreateModel(modelKey),
            TextEncoder = "Qwen3-VL-4B.safetensors",
            Vae = "Qwen2D_VAE.safetensors",
            Prompt = "a mountain lake",
            NegativePrompt = "blurry",
            Seed = 42
        };
    }

    private static IModelViewModel CreateModel(string key)
    {
        var model = new Mock<IModelViewModel>();
        model.SetupProperty(value => value.Key, key);
        return model.Object;
    }

    private static ILoraViewModel CreateLora(string name, double strength)
    {
        var lora = new Mock<ILoraViewModel>();
        lora.SetupGet(value => value.Name).Returns(name);
        lora.SetupProperty(value => value.Strength, strength);
        return lora.Object;
    }

    private static string GetClassType(Dictionary<string, object> workflow, string nodeId)
    {
        return GetNode(workflow, nodeId)["class_type"].ToString()!;
    }

    private static object GetInput(Dictionary<string, object> workflow, string nodeId, string inputName)
    {
        return GetNode(workflow, nodeId)["inputs"] is Dictionary<string, object> inputs
            ? inputs[inputName]
            : throw new InvalidOperationException($"Node {nodeId} has no inputs.");
    }

    private static Dictionary<string, object> GetNode(Dictionary<string, object> workflow, string nodeId)
    {
        return (Dictionary<string, object>)workflow[nodeId];
    }
}