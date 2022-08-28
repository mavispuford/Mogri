namespace MobileDiffusion.Models;

public class TextToImageRequest
{
    /// <summary>
    ///     The text prompt.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    ///     The seed.
    /// </summary>
    public long Seed { get; set; }

    /// <summary>
    ///     The number of outputs.
    /// </summary>
    public short NumberOfOutputs { get; set; }
    
    /// <summary>
    ///     The width (in pixels).
    /// </summary>
    public short Width { get; set; }

    /// <summary>
    ///     The height (in pixels).
    /// </summary>
    public short Height { get; set; }

    /// <summary>
    ///     The number of steps.
    /// </summary>
    public short Steps { get; set; }

    /// <summary>
    ///     The guidance scale (aka CFG_Scale).
    /// </summary>
    public float GuidanceScale { get; set; }
}
