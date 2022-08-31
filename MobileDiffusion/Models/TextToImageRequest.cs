using System.Text.Json.Serialization;

namespace MobileDiffusion.Models;

public class TextToImageRequest
{
    private static Random random = new Random();

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }

    [JsonPropertyName("num_outputs")]
    public int? NumOutputs { get; set; } = 1;

    [JsonPropertyName("num_inference_steps")]
    public string NumInferenceSteps { get; set; } = "50";

    [JsonPropertyName("guidance_scale")]
    public double? GuidanceScale { get; set; } = 7.5;

    [JsonPropertyName("width")]
    public string Width { get; set; } = "512";

    [JsonPropertyName("height")]
    public string Height { get; set; } = "512";

    [JsonPropertyName("init_image")]
    public string InitImage { get; set; }

    [JsonPropertyName("prompt_strength")]
    public double? PromptStrength { get; set; } = .8;

    [JsonPropertyName("seed")]
    public int? Seed { get; set; } = random.Next();
}
