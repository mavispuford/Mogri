namespace Mogri.Interfaces.ViewModels;

/// <summary>
/// Represents an editable text or emoji overlay anchored by the center of its measured bounds.
/// </summary>
public interface ITextElementViewModel : IBaseViewModel
{
    string Id { get; }

    long Order { get; }

    string Text { get; set; }

    float X { get; set; }

    float Y { get; set; }

    float Scale { get; set; }

    float Rotation { get; set; }

    Color Color { get; set; }

    float Alpha { get; set; }

    float BaseFontSize { get; }

    bool IsSelected { get; set; }
}