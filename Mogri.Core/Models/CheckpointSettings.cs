namespace Mogri.Models;

/// <summary>
/// Represents generation settings persisted per checkpoint.
/// </summary>
public class CheckpointSettings
{
    public int Steps { get; set; }
    public double GuidanceScale { get; set; }
    public double? DistilledCfgScale { get; set; }
    public string Sampler { get; set; } = string.Empty;
    public string? Scheduler { get; set; }
    public string? Vae { get; set; }
    public string? TextEncoder { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int BatchCount { get; set; }
    public int BatchSize { get; set; }
    public double DenoisingStrength { get; set; }
    public bool EnableTiling { get; set; }

    public static CheckpointSettings FromPromptSettings(PromptSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new CheckpointSettings
        {
            Steps = settings.Steps,
            GuidanceScale = settings.GuidanceScale,
            DistilledCfgScale = settings.DistilledCfgScale,
            Sampler = settings.Sampler ?? string.Empty,
            Scheduler = settings.Scheduler,
            Vae = settings.Vae,
            TextEncoder = settings.TextEncoder,
            Width = settings.Width,
            Height = settings.Height,
            BatchCount = settings.BatchCount,
            BatchSize = settings.BatchSize,
            DenoisingStrength = settings.DenoisingStrength,
            EnableTiling = settings.EnableTiling
        };
    }

    public void ApplyTo(PromptSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.Steps = Steps;
        settings.GuidanceScale = GuidanceScale;
        settings.DistilledCfgScale = DistilledCfgScale;
        settings.Sampler = Sampler;
        settings.Scheduler = Scheduler;
        settings.Vae = Vae;
        settings.TextEncoder = TextEncoder;
        settings.Width = Width;
        settings.Height = Height;
        settings.BatchCount = BatchCount;
        settings.BatchSize = BatchSize;
        settings.DenoisingStrength = DenoisingStrength;
        settings.EnableTiling = EnableTiling;
    }
}