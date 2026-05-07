using System.ComponentModel;
using System.Buffers.Binary;
using System.Text;
using Mogri.Enums;
using Mogri.Helpers;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.ViewModels;
using Xunit;

namespace Mogri.Tests.Helpers;

public class PngMetadataHelperTests
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Fact]
    public async Task WriteSettings_ThenReadSettings_RoundTrips()
    {
        var original = CreateMinimalPng();
        var settings = new PromptSettings
        {
            Prompt = "a cat",
            NegativePrompt = "blur",
            Steps = 28,
            Sampler = "Euler a",
            Scheduler = "karras",
            GuidanceScale = 6.5,
            Seed = 12345,
            Width = 960,
            Height = 640,
            DenoisingStrength = 0.45,
            ModelType = ModelType.Flux,
            EnableUpscaling = true,
            Upscaler = "R-ESRGAN 4x+",
            UpscaleLevel = 2,
            UpscaleSteps = 15,
            EnableTiling = true,
            Model = new ModelViewModel { DisplayName = "My Model", Key = "my-model" }
        };

        var written = PngMetadataHelper.WriteSettings(original, settings);

        await using var stream = new MemoryStream(written);
        var result = await PngMetadataHelper.ReadSettingsFromStreamAsync(stream);

        Assert.NotNull(result);
        Assert.Equal(settings.Prompt, result.Prompt);
        Assert.Equal(settings.NegativePrompt, result.NegativePrompt);
        Assert.Equal(settings.Steps, result.Steps);
        Assert.Equal(settings.Sampler, result.Sampler);
        Assert.Equal(settings.Scheduler, result.Scheduler);
        Assert.Equal(settings.GuidanceScale, result.GuidanceScale);
        Assert.Equal(settings.Seed, result.Seed);
        Assert.Equal(settings.Width, result.Width);
        Assert.Equal(settings.Height, result.Height);
        Assert.Equal(settings.DenoisingStrength, result.DenoisingStrength);
        Assert.Equal(settings.ModelType, result.ModelType);
        Assert.Equal(settings.EnableUpscaling, result.EnableUpscaling);
        Assert.Equal(settings.Upscaler, result.Upscaler);
        Assert.Equal(settings.UpscaleLevel, result.UpscaleLevel);
        Assert.Equal(settings.UpscaleSteps, result.UpscaleSteps);
        Assert.Equal(settings.EnableTiling, result.EnableTiling);
    }

    [Fact]
    public void WriteSettings_PreservesOriginalImageData()
    {
        var original = CreateMinimalPng();

        var result = PngMetadataHelper.WriteSettings(original, new PromptSettings { Prompt = "a cat" });

        Assert.True(result.AsSpan(0, 8).SequenceEqual(PngSignature));
        var chunkTypes = GetChunkTypes(result).ToList();
        Assert.Contains("IHDR", chunkTypes);
        Assert.Contains("tEXt", chunkTypes);
        Assert.Contains("IEND", chunkTypes);
        Assert.True(result.Length > original.Length);
    }

    [Fact]
    public async Task ReadSettingsFromStreamAsync_NonPngData_ReturnsNull()
    {
        await using var stream = new MemoryStream([1, 2, 3, 4, 5]);

        var result = await PngMetadataHelper.ReadSettingsFromStreamAsync(stream);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReadSettingsFromStreamAsync_PngWithNoTextChunks_ReturnsNull()
    {
        var png = CreateMinimalPng();
        await using var stream = new MemoryStream(png);

        var result = await PngMetadataHelper.ReadSettingsFromStreamAsync(stream);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReadSettingsFromStreamAsync_EmptyStream_ReturnsNull()
    {
        await using var stream = new MemoryStream(Array.Empty<byte>());

        var result = await PngMetadataHelper.ReadSettingsFromStreamAsync(stream);

        Assert.Null(result);
    }

    [Fact]
    public void WriteSettings_NullImageBytes_ReturnsEmptyArray()
    {
        var result = PngMetadataHelper.WriteSettings(null!, new PromptSettings());

        Assert.Empty(result);
    }

    [Fact]
    public void WriteSettings_TooShortImageBytes_ReturnsOriginal()
    {
        var original = new byte[] { 1, 2, 3, 4 };

        var result = PngMetadataHelper.WriteSettings(original, new PromptSettings());

        Assert.Same(original, result);
    }

    [Fact]
    public async Task ReadSettingsFromStreamAsync_LegacyParametersChunk_ParsesViaForgeMetadataParser()
    {
        const string legacy = "a photo of a cat, detailed fur\n" +
            "Negative prompt: blur, noise\n" +
            "Steps: 30, Sampler: DPM++ 2M, CFG scale: 7.0, Seed: 12345, Size: 1024x1024, Denoising strength: 0.5";

        var png = CreateMinimalPng(("parameters", legacy));
        await using var stream = new MemoryStream(png);

        var result = await PngMetadataHelper.ReadSettingsFromStreamAsync(stream);

        Assert.NotNull(result);
        Assert.Equal("a photo of a cat, detailed fur", result.Prompt);
        Assert.Equal("blur, noise", result.NegativePrompt);
        Assert.Equal(30, result.Steps);
        Assert.Equal("DPM++ 2M", result.Sampler);
        Assert.Equal(7.0, result.GuidanceScale);
        Assert.Equal(12345, result.Seed);
        Assert.Equal(1024, result.Width);
        Assert.Equal(1024, result.Height);
        Assert.Equal(0.5, result.DenoisingStrength);
    }

    [Fact]
    public async Task WriteSettings_WithLorasAndPromptStyles_SerializesNestedCollections()
    {
        // Arrange
        var original = CreateMinimalPng();
        var settings = new PromptSettings
        {
            Prompt = "a fox",
            Loras =
            [
                new LoraViewModel { Name = "detail", Alias = "Detail", Strength = 0.8 }
            ],
            PromptStyles =
            [
                new TestPromptStyleViewModel { Name = "Cinematic", Prompt = "cinematic lighting", NegativePrompt = "flat" }
            ]
        };

        // Act
        var written = PngMetadataHelper.WriteSettings(original, settings);
        await using var stream = new MemoryStream(written);
        var result = await PngMetadataHelper.ReadSettingsFromStreamAsync(stream);
        var serializedPng = Encoding.Latin1.GetString(written);

        // Assert
        Assert.NotNull(result);
        var lora = Assert.Single(result.Loras);
        Assert.Equal("detail", lora.Name);
        Assert.Equal("Detail", lora.Alias);
        Assert.Equal(0.8, lora.Strength);
        Assert.Contains("\"styles\":[\"Cinematic\"]", serializedPng);
    }

    private sealed class TestPromptStyleViewModel : IPromptStyleViewModel
    {
        public object? EntityId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Prompt { get; set; } = string.Empty;

        public string NegativePrompt { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }

    private static byte[] CreateMinimalPng(params (string Keyword, string Text)[] textChunks)
    {
        using var stream = new MemoryStream();

        stream.Write(PngSignature);

        // IHDR data: width=1, height=1, bit depth=8, color type=2, compression=0, filter=0, interlace=0
        WriteChunk(stream, "IHDR", [
            0, 0, 0, 1,
            0, 0, 0, 1,
            8,
            2,
            0,
            0,
            0
        ]);

        foreach (var textChunk in textChunks)
        {
            var keywordBytes = Encoding.Latin1.GetBytes(textChunk.Keyword);
            var textBytes = Encoding.Latin1.GetBytes(textChunk.Text);
            var data = new byte[keywordBytes.Length + 1 + textBytes.Length];
            Array.Copy(keywordBytes, 0, data, 0, keywordBytes.Length);
            data[keywordBytes.Length] = 0;
            Array.Copy(textBytes, 0, data, keywordBytes.Length + 1, textBytes.Length);

            WriteChunk(stream, "tEXt", data);
        }

        WriteChunk(stream, "IEND", Array.Empty<byte>());
        return stream.ToArray();
    }

    private static IEnumerable<string> GetChunkTypes(byte[] pngData)
    {
        var offset = 8;
        while (offset + 8 <= pngData.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(pngData.AsSpan(offset, 4));
            var type = Encoding.ASCII.GetString(pngData, offset + 4, 4);
            yield return type;

            offset += 12 + (int)length;
            if (type == "IEND")
            {
                yield break;
            }
        }
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> lengthBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, (uint)data.Length);
        stream.Write(lengthBuffer);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes, 0, typeBytes.Length);

        if (data.Length > 0)
        {
            stream.Write(data, 0, data.Length);
        }

        // CRC is not validated by current parser, but chunk layout requires 4 bytes.
        stream.Write(new byte[4], 0, 4);
    }
}
