using LiteDB;

namespace Mogri.Models;

public class HistoryEntity
{
    public ObjectId Id { get; set; } = null!;

    public string ImageFileName { get; set; } = string.Empty;

    public string ThumbnailFileName { get; set; } = string.Empty;

    public string? UserPrompt { get; set; }

    public string? NegativePrompt { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsHidden { get; set; } = false;
}
