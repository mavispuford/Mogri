namespace MobileDiffusion.Models
{
    /// <summary>
    /// Encapsulates all data required to start a background generation task.
    /// </summary>
    public class GenerationTaskRequest
    {
        /// <summary>
        /// The settings to use for generation. This should be a cloned instance
        /// to prevent UI changes from affecting an in-progress generation.
        /// </summary>
        public required PromptSettings Settings { get; set; }
        
        public int TotalExpectedImages { get; set; }
        
        /// <summary>
        /// A sanitized version of the prompt used for generating safe filenames.
        /// </summary>
        public required string SanitizedPrompt { get; set; }
    }
}
