using System.Text.Json.Serialization;

namespace MobileDiffusion.Models
{
    public class TextToImageResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("output")]
        public List<string> Output { get; set; }
    }
}
