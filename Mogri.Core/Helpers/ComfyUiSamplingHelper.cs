namespace Mogri.Helpers;

/// <summary>
/// Resolves sampler and scheduler labels from other backends to ComfyUI names.
/// </summary>
public static class ComfyUiSamplingHelper
{
    private static readonly IReadOnlyDictionary<string, string> SamplerAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["euler a"] = "euler_ancestral",
            ["euler ancestral"] = "euler_ancestral",
            ["dpm++ 2m"] = "dpmpp_2m",
            ["dpm++ 2m sde"] = "dpmpp_2m_sde",
            ["dpm++ sde"] = "dpmpp_sde",
            ["dpm++ 2s a"] = "dpmpp_2s_ancestral",
            ["dpm2 a"] = "dpm_2_ancestral"
        };

    /// <summary>
    /// Returns the exact server sampler name corresponding to a requested label.
    /// </summary>
    public static string? FindSampler(IEnumerable<string>? availableSamplers, string? requestedSampler)
    {
        if (availableSamplers == null)
        {
            return null;
        }

        var samplers = availableSamplers
            .Where(sampler => !string.IsNullOrWhiteSpace(sampler))
            .ToList();
        if (samplers.Count == 0)
        {
            return null;
        }

        var exactMatch = samplers.FirstOrDefault(sampler =>
            string.Equals(sampler, requestedSampler, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            return exactMatch;
        }

        if (!string.IsNullOrWhiteSpace(requestedSampler) &&
            SamplerAliases.TryGetValue(requestedSampler.Trim(), out var alias))
        {
            return samplers.FirstOrDefault(sampler =>
                string.Equals(sampler, alias, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    /// <summary>
    /// Returns the exact server scheduler name corresponding to a requested label.
    /// </summary>
    public static string? FindScheduler(IEnumerable<string>? availableSchedulers, string? requestedScheduler)
    {
        return availableSchedulers?.FirstOrDefault(scheduler =>
            string.Equals(scheduler, requestedScheduler, StringComparison.OrdinalIgnoreCase));
    }
}