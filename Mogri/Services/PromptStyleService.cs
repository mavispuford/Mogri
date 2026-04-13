using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Maui.Storage;
using Mogri.Interfaces.Services;
using Mogri.Models;

namespace Mogri.Services;

/// <summary>
/// Stores and retrieves prompt styles from local LiteDB storage.
/// </summary>
public class PromptStyleService : IPromptStyleService
{
    private readonly string _dbPath;
    private const string CollectionName = "promptstyles";

    public PromptStyleService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "promptstyles.db");
    }

    private LiteDatabase GetDatabase()
    {
        return new LiteDatabase(_dbPath);
    }

    public Task<List<PromptStyleEntity>> GetAllAsync()
    {
        return Task.Run(() =>
        {
            using var db = GetDatabase();
            var col = db.GetCollection<PromptStyleEntity>(CollectionName);
            col.EnsureIndex(x => x.Name);
            return col.Query()
                .OrderBy(x => x.Name)
                .ToList();
        });
    }

    public Task SaveAsync(PromptStyleEntity entity)
    {
        return Task.Run(() =>
        {
            using var db = GetDatabase();
            var col = db.GetCollection<PromptStyleEntity>(CollectionName);
            col.Upsert(entity);
        });
    }

    public Task DeleteAsync(ObjectId id)
    {
        return Task.Run(() =>
        {
            using var db = GetDatabase();
            var col = db.GetCollection<PromptStyleEntity>(CollectionName);
            col.Delete(id);
        });
    }

    public Task SeedDefaultsIfEmptyAsync()
    {
        return Task.Run(() =>
        {
            using var db = GetDatabase();
            var col = db.GetCollection<PromptStyleEntity>(CollectionName);
            col.EnsureIndex(x => x.Name);

            if (col.Count() > 0)
            {
                return;
            }

            var defaultStyles = new List<PromptStyleEntity>
            {
                new()
                {
                    Name = "Coloring Book",
                    Prompt = "coloring book page, line art, strong outlines, black and white, minimalist, white background, flat vector",
                    NegativePrompt = "color, filled, gradients, solids"
                },
                new()
                {
                    Name = "Professional Photo",
                    Prompt = "Canon DSLR camera, professional photography, sharp focus, studio lighting",
                    NegativePrompt = "amateur, blurry, low quality"
                },
                new()
                {
                    Name = "Cinematic",
                    Prompt = "cinematic lighting, dramatic atmosphere, film grain, color grading",
                    NegativePrompt = "amateur, low budget"
                },
                new()
                {
                    Name = "Watercolor",
                    Prompt = "watercolor painting, soft edges, paper texture, artistic",
                    NegativePrompt = "photorealistic, sharp lines, photo, photography"
                },
                new()
                {
                    Name = "1980s Cartoon",
                    Prompt = "1980s cartoon, VHS, retro, Limited Animation, animation cel, larger than life aesthetic, highly detailed, vibrant colors, airbrushed shadows, intense lighting",
                    NegativePrompt = "photorealistic"
                }
            };

            col.InsertBulk(defaultStyles);
        });
    }
}