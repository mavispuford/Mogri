using System.Text.Json;
using System.Text.Json.Serialization;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.ViewModels;
using SkiaSharp;

namespace Mogri.Json;

/// <summary>
/// Centralizes trim-safe JSON options for persisted app state.
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
        options.Converters.Add(new InterfaceConverter<IModelViewModel, ModelViewModel>());
        options.Converters.Add(new InterfaceListConverter<ILoraViewModel, LoraViewModel>());
        options.Converters.Add(new InterfaceListConverter<IPromptStyleViewModel, PromptStyleViewModel>());

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
[JsonSerializable(typeof(PromptSettings))]
[JsonSerializable(typeof(CheckpointSettings))]
[JsonSerializable(typeof(Dictionary<string, PromptSettings>))]
[JsonSerializable(typeof(LicenseEntry))]
[JsonSerializable(typeof(List<LicenseEntry>))]
[JsonSerializable(typeof(ModelViewModel))]
[JsonSerializable(typeof(LoraViewModel))]
[JsonSerializable(typeof(List<LoraViewModel>))]
[JsonSerializable(typeof(PromptStyleViewModel))]
[JsonSerializable(typeof(List<PromptStyleViewModel>))]
public partial class MogriJsonSerializerContext : JsonSerializerContext
{
}