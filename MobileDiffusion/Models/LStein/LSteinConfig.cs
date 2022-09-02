using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MobileDiffusion.Models.LStein
{
    public class LSteinConfig
    {
        [JsonPropertyName("cfgscale")]
        public string Cfgscale { get; set; }

        [JsonPropertyName("gfpgan_strength")]
        public string GfpganStrength { get; set; }

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
        public string UpscaleStrength { get; set; }

        [JsonPropertyName("width")]
        public string Width { get; set; }
    }
}
