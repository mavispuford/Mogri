using System;
using LiteDB;

namespace Mogri.Models;

public class HistoryEntity
{
    public ObjectId Id { get; set; } = null!;
    public required string ImageFileName { get; set; }
    public required string ThumbnailFileName { get; set; }
    public string? UserPrompt { get; set; }
    public string? NegativePrompt { get; set; }
    public DateTime CreatedAt { get; set; }
}
