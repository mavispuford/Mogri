using Mogri.Helpers;
using Xunit;

namespace Mogri.Tests.Helpers;

public class NoiseHelperTests
{
    [Fact]
    public void NextGaussian_DefaultParameters_MeanApproximatelyZero()
    {
        const int sampleCount = 10_000;
        var random = new Random(42);
        var samples = new double[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = NoiseHelper.NextGaussian(random);
        }

        var mean = samples.Average();

        Assert.InRange(Math.Abs(mean), 0, 0.1);
    }

    [Fact]
    public void NextGaussian_DefaultParameters_StdDevApproximatelyOne()
    {
        const int sampleCount = 10_000;
        var random = new Random(42);
        var samples = new double[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = NoiseHelper.NextGaussian(random);
        }

        var mean = samples.Average();
        var variance = samples.Select(value => Math.Pow(value - mean, 2)).Average();
        var stdDev = Math.Sqrt(variance);

        Assert.InRange(Math.Abs(stdDev - 1.0), 0, 0.1);
    }

    [Fact]
    public void NextGaussian_CustomMeanAndStdDev_ShiftsDistribution()
    {
        const int sampleCount = 10_000;
        var random = new Random(42);
        var samples = new double[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = NoiseHelper.NextGaussian(random, mean: 5, stdDev: 2);
        }

        var mean = samples.Average();
        var variance = samples.Select(value => Math.Pow(value - mean, 2)).Average();
        var stdDev = Math.Sqrt(variance);

        Assert.InRange(Math.Abs(mean - 5), 0, 0.2);
        Assert.InRange(Math.Abs(stdDev - 2), 0, 0.2);
    }
}
