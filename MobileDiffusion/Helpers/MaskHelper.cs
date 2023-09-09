using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MobileDiffusion.Helpers
{
    public static class MaskHelper
    {
        public static SKShader CreateMaskBitmapShaderLines(SKColor paintColor)
        {
            const int tiledBitmapSize = 5;
            var maskTiledBitmap = new SKBitmap(tiledBitmapSize, tiledBitmapSize);

            using var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.None,
                IsAntialias = false,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 10,
                StrokeCap = SKStrokeCap.Round,
                StrokeMiter = 0,
                StrokeJoin = SKStrokeJoin.Round,
                Color = paintColor
            };

            // Make the tiled shader pattern for editor visualization purposes
            for (var x = 0; x < tiledBitmapSize; x++)
            {
                for (var y = 0; y < tiledBitmapSize; y++)
                {
                    if (x == y)
                    {
                        maskTiledBitmap.SetPixel(x, y, paint.Color.WithAlpha(10));
                    }
                    else if (x - 1 == y || x + 1 == y || (x == tiledBitmapSize - 1 && y == 0) || (y == tiledBitmapSize - 1 && x == 0))
                    {
                        maskTiledBitmap.SetPixel(x, y, paint.Color.WithAlpha(50));
                    }
                    else
                    {
                        maskTiledBitmap.SetPixel(x, y, paint.Color.WithAlpha(100));
                    }
                }
            }

            return SKShader.CreateBitmap(maskTiledBitmap, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        }
    }
}
