using System;
using LiteDB;

namespace MobileDiffusion.Models;

public class HistoryEntity
{
    public ObjectId Id { get; set; }
    public string ImageFileName { get; set; }
    public string ThumbnailFileName { get; set; }
    public string UserPrompt { get; set; }
    public string NegativePrompt { get; set; }
    public DateTime CreatedAt { get; set; }
}
