using System;

namespace Mogri.Helpers
{
    public static class NoiseHelper
    {
        /// <summary>
        ///     Generates a number from a Gaussian distribution using the Box-Muller transform.
        /// </summary>
        /// <param name="random">The random number generator.</param>
        /// <param name="mean">The mean of the distribution (defaults to 0).</param>
        /// <param name="stdDev">The standard deviation of the distribution (defaults to 1).</param>
        /// <returns>A random number from the Gaussian distribution.</returns>
        public static double NextGaussian(Random random, double mean = 0, double stdDev = 1)
        {
            // Box-Muller transform
            // u1 and u2 must be in (0, 1] range to avoid Log(0) which returns -Infinity
            double u1 = 1.0 - random.NextDouble();
            double u2 = 1.0 - random.NextDouble();

            double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

            return mean + stdDev * z0;
        }
    }
}
