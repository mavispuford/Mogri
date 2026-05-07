using System.Text.Json;
using System.Text.Json.Serialization;
using Mogri.Models;
using Mogri.ViewModels;
using SkiaSharp;

namespace Mogri.Json;

/// <summary>
/// Centralizes trim-safe JSON options for persisted canvas state.
/// </summary>
public static class MogriJsonSerializer
{
    public static JsonSerializerOptions Options { get; } = createOptions();

    private static JsonSerializerOptions createOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            TypeInfoResolver = MogriJsonSerializerContext.Default
        };

        options.Converters.Add(new ColorJsonConverter());
        options.Converters.Add(new SkPointJsonConverter());

        return options;
    }
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(MaskViewModel))]
[JsonSerializable(typeof(MaskLineViewModel))]
[JsonSerializable(typeof(SegmentationMaskViewModel))]
[JsonSerializable(typeof(SnapshotCanvasActionViewModel))]
[JsonSerializable(typeof(List<MaskLineViewModel>))]
[JsonSerializable(typeof(List<SegmentationMaskViewModel>))]
[JsonSerializable(typeof(List<SnapshotCanvasActionViewModel>))]
[JsonSerializable(typeof(List<SKPoint>))]
public partial class MogriJsonSerializerContext : JsonSerializerContext
{
}