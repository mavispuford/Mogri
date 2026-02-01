using System;
using SkiaSharp;

namespace MobileDiffusion.Helpers
{
    public static class NoiseShaderHelper
    {
        private const int NoiseBitmapSize = 256;
        private static SKBitmap? _baseNoiseBitmap;
        private static readonly object _lock = new();
        private static readonly Random _random = new();

        public static void Initialize()
        {
            Task.Run(() => GenerateNoiseBitmap());
        }

        /// <summary>
        /// Generates a cached 256x256 bitmap with RGB Gaussian noise.
        /// </summary>
        private static SKBitmap GenerateNoiseBitmap()
        {
            lock (_lock)
            {
                if (_baseNoiseBitmap != null)
                {
                    return _baseNoiseBitmap;
                }

                var bmp = new SKBitmap(NoiseBitmapSize, NoiseBitmapSize, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                
                // Use a standard deviation that provides good texture (e.g., 60). 
                // Values are centered at 128. Higher stdDev = Punchier noise.
                const double stdDev = 30.0;

                for (int x = 0; x < NoiseBitmapSize; x++)
                {
                    for (int y = 0; y < NoiseBitmapSize; y++)
                    {
                        // Generate Gaussian noise for R, G, B channels independently for colored noise.
                        // Centered at 128 (middle gray).
                        byte r = (byte)Math.Clamp(128 + NoiseHelper.NextGaussian(_random, 0, stdDev), 0, 255);
                        byte g = (byte)Math.Clamp(128 + NoiseHelper.NextGaussian(_random, 0, stdDev), 0, 255);
                        byte b = (byte)Math.Clamp(128 + NoiseHelper.NextGaussian(_random, 0, stdDev), 0, 255);
                        
                        // Set alpha to 255 (Opaque). The 'Strength' of the noise will be controlled by blending or color filters later.
                        bmp.SetPixel(x, y, new SKColor(r, g, b, 255));
                    }
                }

                _baseNoiseBitmap = bmp;
                return _baseNoiseBitmap;
            }
        }

        /// <summary>
        /// Create a shader that blends the provided <paramref name="color"/> with a tiled noise texture.
        /// Strength is in the range [0,1] and controls how pronounced the noise is.
        /// Returns null when strength is less than or equal to 0.
        /// </summary>
        public static SKShader? CreateNoiseShader(SKColor color, double strength)
        {
            if (strength <= 0)
            {
                return null;
            }

            strength = Math.Max(0, Math.Min(1, strength));

            var baseNoise = GenerateNoiseBitmap();

            // 1. Create a tiled shader from the base noise bitmap
            using var noiseBitmapShader = SKShader.CreateBitmap(baseNoise, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);

            // 2. Adjust the intensity/contrast of the noise using a ColorMatrix.
            //    We want to scale the noise around 128.
            //    If strength = 0, R=128 (Neutral Gray). If strength = 1, R=Original Noise.
            //    ColorMatrix formula: R' = R * s + offset
            //    We want 128 * s + offset = 128  => offset = 128(1-s).
            //    (assuming 0-255 range. Skia color matrix values are typically multiplied by unnormalized or normalized values depending on context,
            //    but usually the offset (5th column) is added directly. For normalized (0..1), offset is 0.5(1-s)).
            
            float s = (float)strength;
            // Use 0-1 range logic to create the offset.
            // 0.5 corresponds to 128 (Neutral Gray).
            float normOffset = 0.5f * (1 - s); 

            // Matrix to scale contrast around 0.5 (Gray)
            var matrix = new float[]
            {
                s, 0, 0, 0, normOffset,
                0, s, 0, 0, normOffset,
                0, 0, s, 0, normOffset,
                0, 0, 0, 1, 0
            };

            using var filter = SKColorFilter.CreateColorMatrix(matrix);
            using var adjustedNoiseShader = noiseBitmapShader.WithColorFilter(filter);

            // 3. Create a solid color shader for the brush color.
            //    We use the color with full opacity here because the Paint object handles the overall stroke alpha.
            using var colorShader = SKShader.CreateColor(color.WithAlpha(255));

            // 4. Compose the Color and Noise.
            //    We use 'HardLight' which resembles the arithmetic addition/subtraction.
            //    It darkens bright colors when noise is dark, and lightens dark colors when noise is light.
            
            return SKShader.CreateCompose(colorShader, adjustedNoiseShader, SKBlendMode.HardLight);
        }
    }
}
