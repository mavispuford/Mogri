using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MobileDiffusion.Helpers;

public static class PngMetadataHelper
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static async Task<(string? Positive, string? Negative, string? Raw)> ReadParametersAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return (null, null, null);

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await ReadParametersFromStreamAsync(stream);
        }
        catch (Exception)
        {
            // Log or handle exception as needed
            return (null, null, null);
        }
    }

    public static async Task<(string? Positive, string? Negative, string? Raw)> ReadParametersFromStreamAsync(Stream stream)
    {
        var buffer = new byte[8];
        if (await stream.ReadAsync(buffer, 0, 8) != 8 || !UnsafeCompare(buffer, PngSignature))
        {
            return (null, null, null);
        }

        while (stream.Position < stream.Length)
        {
            // Read Length (4 bytes)
            if (await stream.ReadAsync(buffer, 0, 4) != 4) break;
            var length = BinaryPrimitives.ReadUInt32BigEndian(buffer);

            // Read Type (4 bytes)
            if (await stream.ReadAsync(buffer, 0, 4) != 4) break;
            var type = Encoding.ASCII.GetString(buffer, 0, 4);

            if (type == "tEXt")
            {
                // Read Data
                var data = new byte[length];
                if (await stream.ReadAsync(data, 0, (int)length) != length) break;

                // tEXt format: Keyword + null + Text
                var dataStr = Encoding.Latin1.GetString(data);
                var nullIndex = dataStr.IndexOf('\0');
                if (nullIndex > 0)
                {
                    var keyword = dataStr.Substring(0, nullIndex);
                    if (keyword == "parameters")
                    {
                        var text = dataStr.Substring(nullIndex + 1);
                        return ParseStableDiffusionParameters(text);
                    }
                }
            }
            else
            {
                // Skip data
                stream.Seek(length, SeekOrigin.Current);
            }

            // Skip CRC (4 bytes)
            stream.Seek(4, SeekOrigin.Current);
        }

        return (null, null, null);
    }

    private static (string? Positive, string? Negative, string? Raw) ParseStableDiffusionParameters(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
            return (null, null, parameters);

        string? positive = null;
        string? negative = null;

        var parts = parameters.Split(new[] { "Negative prompt:" }, StringSplitOptions.None);
        if (parts.Length > 0)
        {
            positive = parts[0].Trim();
        }

        if (parts.Length > 1)
        {
            // The negative prompt section often ends with "Steps:" or other generation params
            var negativeSection = parts[1];
            var stepsIndex = negativeSection.IndexOf("\nSteps:", StringComparison.Ordinal);

            if (stepsIndex == -1)
            {
                // Try searching for just "Steps:" without newline if formatting varies
                stepsIndex = negativeSection.IndexOf("Steps:", StringComparison.Ordinal);
            }

            if (stepsIndex != -1)
            {
                negative = negativeSection.Substring(0, stepsIndex).Trim();
            }
            else
            {
                negative = negativeSection.Trim();
            }
        }

        // Clean up weird trailing stuff on positive prompt if negative prompt wasn't found but steps were
        if (negative == null && positive != null)
        {
            var stepsIndex = positive.IndexOf("\nSteps:", StringComparison.Ordinal);
            if (stepsIndex != -1)
            {
                positive = positive.Substring(0, stepsIndex).Trim();
            }
        }

        return (positive, negative, parameters);
    }

    private static bool UnsafeCompare(byte[] a1, byte[] a2)
    {
        if (a1 == null || a2 == null || a1.Length != a2.Length)
            return false;
        for (int i = 0; i < a1.Length; i++)
            if (a1[i] != a2[i]) return false;
        return true;
    }
}
