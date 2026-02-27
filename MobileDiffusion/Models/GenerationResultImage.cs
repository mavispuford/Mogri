namespace MobileDiffusion.Models
{
    /// <summary>
    /// Represents a single successfully generated image from a task.
    /// </summary>
    public class GenerationResultImage
    {
        public required string InternalUri { get; set; }
        public required PromptSettings Settings { get; set; }
        public required ApiResponse Response { get; set; }
        public int ImageNumber { get; set; }
    }
}
