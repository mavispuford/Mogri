using System.Text.Json.Serialization;
using Mogri.Models;

namespace Mogri.Json;

/// <summary>
/// Provides trim-safe metadata for PNG prompt settings serialization.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(PngMetadataDto))]
[JsonSerializable(typeof(PngMetadataDto.LoraEntry))]
[JsonSerializable(typeof(List<PngMetadataDto.LoraEntry>))]
[JsonSerializable(typeof(List<string>))]
public partial class PngMetadataJsonSerializerContext : JsonSerializerContext
{
}