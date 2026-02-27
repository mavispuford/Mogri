using System.Collections.Generic;

namespace MobileDiffusion.Models
{
    /// <summary>
    /// Represents the final outcome of a generation task, including all generated images
    /// or any errors that occurred.
    /// </summary>
    public class GenerationTaskResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<GenerationResultImage> Images { get; set; } = new();
    }
}
