using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MobileDiffusion.Models.LStein
{
    public class LSteinResponseItem
    {
        [JsonPropertyName("event")]
        public string Event { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("seed")]
        public long Seed { get; set; }

        [JsonPropertyName("config")]
        public LSteinConfig Config { get; set; }
    }
}
