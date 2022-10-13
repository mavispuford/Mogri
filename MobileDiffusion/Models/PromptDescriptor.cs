namespace MobileDiffusion.Models;

internal class PromptDescriptor
{
    public string Text { get; set; }

    public IList<string> Tags { get; set; }

    public string TagsString => Tags != null && Tags.Any() ? string.Join(", ", Tags) : string.Empty;
}
