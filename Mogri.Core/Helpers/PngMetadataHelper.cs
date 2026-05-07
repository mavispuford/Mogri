using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using Mogri.Json;
using System.Threading.Tasks;
using Mogri.Models;
using Mogri.Services;

namespace Mogri.Helpers;

/// <summary>
/// Helper for reading and writing image generation metadata to PNG files.
/// Prioritizes the custom JSON format ("md_settings") but maintains backward
/// compatibility with the legacy A1111/Forge plain text format ("parameters").
/// </summary>
public static class PngMetadataHelper
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>
    /// Reads prompt settings from a PNG file, prioritizing the JSON "md_settings" chunk
    /// before falling back to the legacy "parameters" text chunk.
    /// </summary>
    public static async Task<PromptSettings?> ReadSettingsAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await ReadSettingsFromStreamAsync(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads prompt settings from a PNG stream, prioritizing the JSON "md_settings" chunk
    /// before falling back to the legacy "parameters" text chunk.
    /// </summary>
    public static async Task<PromptSettings?> ReadSettingsFromStreamAsync(Stream stream)
    {
        var buffer = new byte[8];
        if (await stream.ReadAsync(buffer, 0, 8) != 8 || !buffer.AsSpan().SequenceEqual(PngSignature))
        {
            return null;
        }

        string? jsonSettings = null;
        string? legacyParameters = null;

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
                    var text = dataStr.Substring(nullIndex + 1);

                    if (keyword == "md_settings")
                    {
                        jsonSettings = text;
                        // We found our preferred format, we can stop scanning if we want to prioritize speed
                        // But if we want to ensure we get *latest* valid data? 
                        // Usually file only has one. Let's break.
                        break; 
                    }
                    else if (keyword == "parameters")
                    {
                        legacyParameters = text;
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

        // 1. Try JSON format
        if (!string.IsNullOrEmpty(jsonSettings))
        {
            try
            {
                var dto = JsonSerializer.Deserialize(jsonSettings, PngMetadataJsonSerializerContext.Default.PngMetadataDto);
                if (dto != null)
                {
                    return dto.ToPromptSettings();
                }
            }
            catch (JsonException ex)
            {
                // Fallback to legacy if parsing failed
                System.Diagnostics.Debug.WriteLine($"Failed to parse md_settings JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error reading md_settings: {ex.Message}");
            }
        }

        // 2. Fallback to legacy format
        if (!string.IsNullOrEmpty(legacyParameters))
        {
            return ForgeMetadataParser.Parse(legacyParameters);
        }

        return null;
    }


    /// <summary>
    /// Writes PromptSettings as a JSON "md_settings" chunk into the PNG image.
    /// </summary>
    public static byte[] WriteSettings(byte[] originalImage, PromptSettings settings)
    {
        try
        {
            var dto = PngMetadataDto.FromPromptSettings(settings);
            // System.Text.Json default escapes non-ASCII characters to \uXXXX, 
            // which is safe for Latin-1 encoding (see https://www.w3.org/TR/PNG/#11tEXt)
            var json = JsonSerializer.Serialize(dto, PngMetadataJsonSerializerContext.Default.PngMetadataDto);
            return InsertTextChunk(originalImage, "md_settings", json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write md_settings: {ex.Message}");
            return originalImage;
        }
    }

    private static byte[] InsertTextChunk(byte[] originalImage, string keyword, string text)
    {
        // Simple check for valid PNG
        if (originalImage == null || originalImage.Length < 33 || !originalImage.AsSpan(0, 8).SequenceEqual(PngSignature))
        {
            return originalImage ?? Array.Empty<byte>();
        }

        var keywordBytes = Encoding.Latin1.GetBytes(keyword);
        var textBytes = Encoding.Latin1.GetBytes(text);
        
        // Length of data = keyword + null separator + text
        var dataLength = keywordBytes.Length + 1 + textBytes.Length;
        var chunkLength = 4 + 4 + dataLength + 4; // Length + Type + Data + CRC

        var chunk = new byte[chunkLength];
        
        // Write Length
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(0, 4), (uint)dataLength);
        
        // Write Type
        Encoding.ASCII.GetBytes("tEXt", 0, 4, chunk, 4);
        
        // Write Data
        Array.Copy(keywordBytes, 0, chunk, 8, keywordBytes.Length);
        chunk[8 + keywordBytes.Length] = 0; // Null separator
        Array.Copy(textBytes, 0, chunk, 8 + keywordBytes.Length + 1, textBytes.Length);
        
        // Calculate CRC on Type + Data
        var crc = System.IO.Hashing.Crc32.HashToUInt32(chunk.AsSpan(4, 4 + dataLength));
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(chunkLength - 4, 4), crc);

        // Insert after IHDR (assumed to be at index 33)
        // Signature (8) + IHDR Length (4) + IHDR Type (4) + IHDR Data (13) + IHDR CRC (4) = 33
        var result = new byte[originalImage.Length + chunkLength];
        Array.Copy(originalImage, 0, result, 0, 33);
        Array.Copy(chunk, 0, result, 33, chunkLength);
        Array.Copy(originalImage, 33, result, 33 + chunkLength, originalImage.Length - 33);

        return result;
    }

}
