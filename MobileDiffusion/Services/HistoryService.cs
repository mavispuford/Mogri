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

    public async Task InitializeAsync()
    {
        await Task.Run(async () =>
        {
            using var db = GetDatabase();
            var col = db.GetCollection<HistoryEntity>(CollectionName);

            // Ensure indexes
            col.EnsureIndex(x => x.UserPrompt);
            col.EnsureIndex(x => x.ImageFileName);
            col.EnsureIndex(x => x.CreatedAt);

            // Get all files from cache directory (source of truth)
            var cacheDir = FileSystem.CacheDirectory;
            if (!Directory.Exists(cacheDir))
                return;

            var imageFiles = Directory.GetFiles(cacheDir, "*.png")
                .Where(f => !Path.GetFileName(f).StartsWith(Constants.ThumbnailPrefix))
                .Select(f => new FileInfo(f))
                .ToList();

            var dbEntries = col.FindAll().ToDictionary(x => x.ImageFileName);

            // 1. Prune: Remove DB entries that don't exist on disk
            var filesSet = new HashSet<string>(imageFiles.Select(x => x.FullName));
            var toRemove = dbEntries.Values.Where(e => !filesSet.Contains(e.ImageFileName)).ToList();

            foreach (var item in toRemove)
            {
                col.Delete(item.Id);
            }

            // 2. Insert: Add new files to DB
            foreach (var fileInfo in imageFiles)
            {
                if (!dbEntries.ContainsKey(fileInfo.FullName))
                {
                    var (positive, negative, raw) = await PngMetadataHelper.ReadParametersAsync(fileInfo.FullName);

                    var entity = new HistoryEntity
                    {
                        ImageFileName = fileInfo.FullName,
                        ThumbnailFileName = Path.Combine(cacheDir, Constants.ThumbnailPrefix + fileInfo.Name), // Approximate, standard naming convention
                        UserPrompt = positive ?? "",
                        NegativePrompt = negative ?? "",
                        CreatedAt = fileInfo.CreationTime
                    };

                    col.Insert(entity);
                }
            }
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
                // LiteDB simple text search or contains
                // Using Lower() for case insensitive search if not using FreeText search
                // For simple "contains", we can use LINQ expression
                var lowerQuery = query.ToLower();
                result = col.Query()
                    .Where(x => (x.UserPrompt != null && x.UserPrompt.ToLower().Contains(lowerQuery)) ||
                                (x.NegativePrompt != null && x.NegativePrompt.ToLower().Contains(lowerQuery)))
                    .OrderByDescending(x => x.CreatedAt)
                    .Skip(skip)
                    .Limit(take)
                    .ToEnumerable();
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
}
