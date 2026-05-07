using CommunityToolkit.Mvvm.ComponentModel;
using Mogri.Interfaces.ViewModels;
using System.Text.Json.Serialization;

namespace Mogri.ViewModels;

/// <summary>
/// Represents an editable text or emoji overlay anchored by the center of its measured bounds.
/// </summary>
public partial class TextElementViewModel : BaseViewModel, ITextElementViewModel
{
    private const float DefaultBaseFontSize = 96f;

    public string Id { get; init; }

    public long Order { get; init; }

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the image-space center X of the measured shaped-text bounds.
    /// </summary>
    [ObservableProperty]
    public partial float X { get; set; }

    /// <summary>
    /// Gets or sets the image-space center Y of the measured shaped-text bounds.
    /// </summary>
    [ObservableProperty]
    public partial float Y { get; set; }

    [ObservableProperty]
    public partial float Scale { get; set; } = 1f;

    [ObservableProperty]
    public partial float Rotation { get; set; }

    [ObservableProperty]
    public partial Color Color { get; set; } = Colors.White;

    [ObservableProperty]
    public partial float Alpha { get; set; } = 1f;

    public float BaseFontSize { get; init; } = DefaultBaseFontSize;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public TextElementViewModel()
        : this(Guid.NewGuid().ToString(), 0)
    {
    }

    public TextElementViewModel(long order)
        : this(Guid.NewGuid().ToString(), order)
    {
    }

    [JsonConstructor]
    public TextElementViewModel(string id, long order, float baseFontSize = DefaultBaseFontSize)
    {
        Id = id;
        Order = order;
        BaseFontSize = baseFontSize;
    }
}