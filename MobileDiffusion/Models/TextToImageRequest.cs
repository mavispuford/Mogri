namespace MobileDiffusion.Models;

public class TextToImageRequest
{
    private static Random random = new Random();

    /// <summary>
    ///     The text prompt.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    ///     The seed.
    /// </summary>
    public long Seed { get; set; } = random.NextInt64();

    /// <summary>
    ///     The number of outputs.
    /// </summary>
    public short NumberOfOutputs { get; set; } = 1;

    /// <summary>
    ///     The width (in pixels).
    /// </summary>
    public short Width { get; set; } = 512;

    /// <summary>
    ///     The height (in pixels).
    /// </summary>
    public short Height { get; set; } = 512;

    /// <summary>
    ///     The number of steps.
    /// </summary>
    public short Steps { get; set; } = 50;

    /// <summary>
    ///     The guidance scale (aka CFG_Scale).
    /// </summary>
    public float GuidanceScale { get; set; } = 7.5f;
}
