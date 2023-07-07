using Newtonsoft.Json;

namespace MobileDiffusion.Models.Automatic1111;

/// <summary>
///     The generated sdapi/v1/loras GET method returns an object so convert to this.
/// </summary>
public class LoraItem
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("alias")]
    public string Alias { get; set; }

    [JsonProperty("path")]
    public string Path { get; set; }

    [JsonProperty("metadata")]
    public Dictionary<string, object> Metadata { get; set; }
}
