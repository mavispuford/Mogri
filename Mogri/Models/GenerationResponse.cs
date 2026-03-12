using System.Collections.Generic;

namespace Mogri.Models
{
    public class GenerationResponse
    {
        public List<string>? Images { get; set; }
        public string? Info { get; set; }
        public List<long>? Seeds { get; set; }
    }
}
