using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using MobileDiffusion.Helpers;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Models;
using Microsoft.Maui.Storage;

namespace MobileDiffusion.Services;

public class HistoryService : IHistoryService
{
    private readonly string _dbPath;
    private readonly IFileService _fileService;
    private const string CollectionName = "history";

    public HistoryService(IFileService fileService)
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "history.db");
        _fileService = fileService;
    }

    private LiteDatabase GetDatabase()
    {
        return new LiteDatabase(_dbPath);
    }

    public async Task<bool> InitializeAsync()
    {
        return await Task.Run(async () =>
        {
            using var db = GetDatabase();
            var col = db.GetCollection<HistoryEntity>(CollectionName);
            bool hasChanges = false;

            // Ensure indexes
            col.EnsureIndex(x => x.UserPrompt);
            col.EnsureIndex(x => x.ImageFileName);
            col.EnsureIndex(x => x.CreatedAt);

            // Get all files from cache directory (source of truth)
            var cacheDir = FileSystem.CacheDirectory;
            if (!Directory.Exists(cacheDir))
                return false;

            // Use EnumerateFiles for lower memory footprint, though we still need to realize a list for comparison
            // But projection helps.
            var imageFiles = Directory.GetFiles(cacheDir, "*.png");

            // Files on disk map
            var diskFilesMap = new Dictionary<string, FileInfo>();
            foreach (var f in imageFiles)
            {
                if (!Path.GetFileName(f).StartsWith(Constants.ThumbnailPrefix))
                {
                    diskFilesMap[f] = new FileInfo(f);
                }
            }

            // Projection - read only ImageFileName from DB to avoid loading full prompts/history into memory
            // We map ImageFileName -> Id
            var dbEntries = col.Query()
                .Select(x => new { x.ImageFileName, x.Id })
                .ToEnumerable()
                .ToDictionary(x => x.ImageFileName, x => x.Id);

            // 1. Prune: Remove DB entries that don't exist on disk
            var toRemove = new List<ObjectId>();
            foreach (var kvp in dbEntries)
            {
                if (!diskFilesMap.ContainsKey(kvp.Key))
                {
                    toRemove.Add(kvp.Value);
                }
            }

            if (toRemove.Count > 0)
            {
                hasChanges = true;
                foreach (var id in toRemove)
                {
                    col.Delete(id);
                }
            }

            // 2. Insert: Add new files to DB
            foreach (var kvp in diskFilesMap)
            {
                var filePath = kvp.Key;
                if (!dbEntries.ContainsKey(filePath))
                {
                    try
                    {
                        var fileInfo = kvp.Value;
                        // Avoid holding DB lock during IO
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            // Process insertions in batch to minimize DB lock time and ensure transaction safety
            var newEntities = new List<HistoryEntity>();

            foreach (var kvp in diskFilesMap)
            {
                var filePath = kvp.Key;
                if (!dbEntries.ContainsKey(filePath))
                {
                    try
                    {
                        var fileInfo = kvp.Value;
                        string positive = "";
                        string negative = "";

                        var settings = await PngMetadataHelper.ReadSettingsAsync(filePath);
                        if (settings != null)
                        {
                            positive = settings.Prompt;
                            negative = settings.NegativePrompt;
                        }

                        var entity = new HistoryEntity
                        {
                            ImageFileName = filePath,
                            ThumbnailFileName = Path.Combine(cacheDir, Constants.ThumbnailPrefix + fileInfo.Name), // Approximate, standard naming convention
                            UserPrompt = positive,
                            NegativePrompt = negative,
                            CreatedAt = fileInfo.CreationTime
                        };
                        newEntities.Add(entity);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to process history file {filePath}: {ex}");
                    }
                }
            }

            if (newEntities.Count > 0)
            {
                hasChanges = true;
                col.InsertBulk(newEntities);
            }

            return hasChanges;
        });
    }

    public Task<IEnumerable<HistoryEntity>> SearchAsync(string query, int skip, int take)
    {
        return Task.Run(() =>
        {
            using var db = GetDatabase();
            var col = db.GetCollection<HistoryEntity>(CollectionName);
            var result = Enumerable.Empty<HistoryEntity>();

            if (string.IsNullOrWhiteSpace(query))
            {
                result = col.Query()
                    .OrderByDescending(x => x.CreatedAt)
                    .Skip(skip)
                    .Limit(take)
                    .ToEnumerable();
            }
            else
            {
                var lowerQuery = query.ToLower();
                bool isExact = false;
                var cleanQuery = lowerQuery;

                if (lowerQuery.Length > 2 && ((lowerQuery.StartsWith("\"") && lowerQuery.EndsWith("\"")) || (lowerQuery.StartsWith("'") && lowerQuery.EndsWith("'"))))
                {
                    isExact = true;
                    cleanQuery = lowerQuery.Substring(1, lowerQuery.Length - 2);
                }

                // Fetch all matching records to sort them by relevance in memory
                var candidates = col.Query()
                    .Where(x => (x.UserPrompt != null && x.UserPrompt.ToLower().Contains(cleanQuery)) ||
                                (x.NegativePrompt != null && x.NegativePrompt.ToLower().Contains(cleanQuery)))
                    .ToEnumerable();

                if (isExact)
                {
                    candidates = candidates.Where(x => IsWholeWordMatch(x.UserPrompt ?? string.Empty, cleanQuery) || IsWholeWordMatch(x.NegativePrompt ?? string.Empty, cleanQuery));
                }

                result = candidates
                    .Select(x =>
                    {
                        var score1 = GetStringScore(x.UserPrompt ?? string.Empty, cleanQuery);
                        var score2 = GetStringScore(x.NegativePrompt ?? string.Empty, cleanQuery);
                        var finalScore = Math.Min(score1, score2);
                        return new { Item = x, Score = finalScore };
                    })
                    .OrderBy(x => x.Score)
                    .ThenByDescending(x => x.Item.CreatedAt)
                    .Skip(skip)
                    .Take(take) // Using Take instead of Limit for IEnumerable
                    .Select(x => x.Item);
            }

            // Materialize list before disposing DB
            return (IEnumerable<HistoryEntity>)result.ToList();
        });
    }

    public async Task DeleteItemsAsync(IEnumerable<HistoryEntity> items)
    {
        // Delete files first
        foreach (var item in items)
        {
            if (_fileService != null)
            {
                await _fileService.DeleteFileFromInternalStorage(item.ImageFileName);
                await _fileService.DeleteFileFromInternalStorage(item.ThumbnailFileName);
            }
        }

        // Then delete from DB (offload to thread pool as LiteDB is sync)
        await Task.Run(() =>
        {
            using var db = GetDatabase();
            var col = db.GetCollection<HistoryEntity>(CollectionName);

            foreach (var item in items)
            {
                col.Delete(item.Id);
            }
        });
    }

    private bool IsWholeWordMatch(string text, string lowerQuery)
    {
        if (string.IsNullOrEmpty(text)) return false;

        var lowerText = text.ToLower();
        int index = lowerText.IndexOf(lowerQuery, StringComparison.Ordinal);

        while (index != -1)
        {
            bool startOk = index == 0 || !char.IsLetterOrDigit(lowerText[index - 1]);
            bool endOk = (index + lowerQuery.Length == lowerText.Length) || !char.IsLetterOrDigit(lowerText[index + lowerQuery.Length]);

            if (startOk && endOk) return true;

            index = lowerText.IndexOf(lowerQuery, index + 1, StringComparison.Ordinal);
        }
        return false;
    }

    private int GetStringScore(string text, string lowerQuery)
    {
        if (string.IsNullOrEmpty(text)) return 3;

        if (text.StartsWith(lowerQuery, StringComparison.OrdinalIgnoreCase)) return 0;

        var lowerText = text.ToLower();
        int index = lowerText.IndexOf(lowerQuery, StringComparison.Ordinal);

        if (index == -1) return 3;

        while (index != -1)
        {
            if (index > 0 && !char.IsLetterOrDigit(lowerText[index - 1]))
            {
                return 1;
            }
            index = lowerText.IndexOf(lowerQuery, index + 1, StringComparison.Ordinal);
        }

        return 2;
    }
}
