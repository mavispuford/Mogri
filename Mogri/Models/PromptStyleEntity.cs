using LiteDB;

namespace Mogri.Models;

/// <summary>
/// Represents a locally persisted prompt style.
/// </summary>
public class PromptStyleEntity
{
    public ObjectId Id { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
}