using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MobileDiffusion.Models.LStein
{
    public class LSteinRequest
    {
        [JsonPropertyName("cfgscale")]
        public string Cfgscale { get; set; }

        [JsonPropertyName("gfpgan_strength")]
        public string GfpganStrength { get; set; } = "0.8";

        [JsonPropertyName("height")]
        public string Height { get; set; }

        [JsonPropertyName("initimg")]
        public string Initimg { get; set; }

        [JsonPropertyName("iterations")]
        public string Iterations { get; set; }
        
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; }

        [JsonPropertyName("sampler")]
        public string Sampler { get; set; }

        [JsonPropertyName("seed")]
        public string Seed { get; set; }

        [JsonPropertyName("steps")]
        public string Steps { get; set; }

        [JsonPropertyName("strength")]
        public string Strength { get; set; }

        [JsonPropertyName("upscale_level")]
        public string UpscaleLevel { get; set; }

        [JsonPropertyName("upscale_strength")]
        public string UpscaleStrength { get; set; } = ".75";

        [JsonPropertyName("width")]
        public string Width { get; set; }
        
        public static LSteinRequest FromBaseRequest(BaseRequest baseRequest)
        {
            var result = new LSteinRequest()
            {
                Cfgscale = baseRequest.GuidanceScale.ToString(),
                Height = baseRequest.Height.ToString(),
                Initimg = baseRequest.InitImage,
                Iterations = baseRequest.NumOutputs.ToString(),
                Prompt = baseRequest.Prompt,
                Sampler = baseRequest.Sampler.ToString(),
                Seed = baseRequest.Seed.ToString(),
                Steps = baseRequest.NumInferenceSteps.ToString(),
                Strength = baseRequest.PromptStrength.ToString(),
                Width = baseRequest.Width.ToString(),
            };

            result.UpscaleLevel = String.Empty;

            return result;
        }
    }
}
