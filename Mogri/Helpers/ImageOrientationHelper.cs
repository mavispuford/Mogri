using SkiaSharp;

namespace Mogri.Helpers;

/// <summary>
/// Applies EXIF orientation metadata to image pixel data so downstream consumers
/// display images in their intended orientation. SkiaSharp does not apply EXIF
/// orientation during decode, so this must be called on streams before they are
/// consumed.
/// </summary>
public static class ImageOrientationHelper
{
    /// <summary>
    /// Returns a stream with EXIF orientation baked into the pixel data.
    /// If rotation was needed, the image is re-encoded as JPEG (wasRotated = true).
    /// If not, the original bytes are returned in a seekable MemoryStream (wasRotated = false).
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    public static (MemoryStream Stream, bool WasRotated) ApplyExifOrientation(Stream inputStream)
    {
        var memStream = copyToMemoryStream(inputStream);

        // SKCodec.Create(Stream) takes ownership and will dispose the stream,
        // so create from SKData instead to keep memStream independent.
        using var skData = SKData.CreateCopy(memStream.ToArray());
        using var codec = SKCodec.Create(skData);

        if (codec == null)
        {
            memStream.Seek(0, SeekOrigin.Begin);
            return (memStream, false);
        }

        var origin = codec.EncodedOrigin;
        if (origin == SKEncodedOrigin.TopLeft)
        {
            memStream.Seek(0, SeekOrigin.Begin);
            return (memStream, false);
        }

        var info = new SKImageInfo(
            codec.Info.Width, codec.Info.Height,
            codec.Info.ColorType, SKAlphaType.Unpremul, codec.Info.ColorSpace);

        using var bitmap = SKBitmap.Decode(codec, info);

        if (bitmap == null)
        {
            memStream.Seek(0, SeekOrigin.Begin);
            return (memStream, false);
        }

        using var rotated = autoOrient(bitmap, origin);
        var output = new MemoryStream();

        using var image = SKImage.FromBitmap(rotated);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        data.SaveTo(output);

        memStream.Dispose();
        output.Seek(0, SeekOrigin.Begin);
        return (output, true);
    }

    /// <summary>
    /// Returns a new SKBitmap with the specified EXIF orientation transform applied.
    /// The caller is responsible for disposing the returned bitmap.
    /// </summary>
    private static SKBitmap autoOrient(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        bool swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                    or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int w = swap ? bitmap.Height : bitmap.Width;
        int h = swap ? bitmap.Width : bitmap.Height;

        var result = new SKBitmap(w, h, bitmap.ColorType, bitmap.AlphaType);
        using var canvas = new SKCanvas(result);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight: // Mirror horizontal
                canvas.Translate(w, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight: // Rotate 180
                canvas.Translate(w, h);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft: // Mirror vertical
                canvas.Translate(0, h);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop: // Transpose
                canvas.Scale(-1, 1);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightTop: // Rotate 90 CW
                canvas.Translate(w, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom: // Transverse
                canvas.Translate(w, h);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom: // Rotate 270 CW
                canvas.Translate(0, h);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(bitmap, 0, 0);
        canvas.Flush();

        return result;
    }

    private static MemoryStream copyToMemoryStream(Stream input)
    {
        var output = new MemoryStream();
        input.CopyTo(output);
        return output;
    }
}
