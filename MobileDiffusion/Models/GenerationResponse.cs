using System.Collections.Generic;

namespace MobileDiffusion.Models
{
    public class GenerationResponse
    {
        public List<string> Images { get; set; }
        public string Info { get; set; }
    }
}
