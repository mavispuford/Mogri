using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Mogri.Interfaces.Services;
using Mogri.Models;
using Mogri.ViewModels;
using SkiaSharp;
using Mogri.Json;

namespace Mogri.Services;

/// <summary>
/// Implements canvas history tracking by writing bitmap snapshots and serialized 
/// canvas actions to the file system's CacheDirectory.
/// </summary>
public class CanvasHistoryService : ICanvasHistoryService
{
    private const int MaxSnapshots = 15;
    private readonly string _snapshotDirectory;
    private readonly Dictionary<string, CanvasSnapshot> _snapshots = new();
    private readonly List<string> _insertionOrder = new();
    private readonly JsonSerializerOptions _jsonOptions;

    public CanvasHistoryService()
    {
        _snapshotDirectory = Path.Combine(FileSystem.CacheDirectory, "canvas_history");
        if (!Directory.Exists(_snapshotDirectory))
        {
            Directory.CreateDirectory(_snapshotDirectory);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
        };
        _jsonOptions.Converters.Add(new ColorJsonConverter());
    }

    public int Count => _snapshots.Count;

    public async Task<string> SaveSnapshotAsync(SKBitmap bitmap, IList<CanvasActionViewModel>? canvasActions = null)
    {
        string snapshotId = Guid.NewGuid().ToString();
        string bitmapFilePath = Path.Combine(_snapshotDirectory, $"{snapshotId}.png");
        string? actionsFilePath = null;

        await Task.Run(() =>
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(bitmapFilePath);
            data.SaveTo(stream);
        });

        if (canvasActions != null && canvasActions.Count > 0)
        {
            actionsFilePath = Path.Combine(_snapshotDirectory, $"{snapshotId}.actions.json");
            
            var maskLineViewModels = canvasActions.OfType<MaskLineViewModel>().ToList();
            var segmentationMaskViewModels = canvasActions.OfType<SegmentationMaskViewModel>().ToList();

            var wrapper = new MaskViewModel
            {
                Lines = maskLineViewModels,
                SegmentationMasks = segmentationMaskViewModels
            };

            string json = JsonSerializer.Serialize(wrapper, _jsonOptions);
            await File.WriteAllTextAsync(actionsFilePath, json);
        }

        var snapshot = new CanvasSnapshot
        {
            BitmapFilePath = bitmapFilePath,
            ActionsFilePath = actionsFilePath
        };

        _snapshots[snapshotId] = snapshot;
        _insertionOrder.Add(snapshotId);

        await enforceSnapshotLimitAsync();

        return snapshotId;
    }

    public async Task<(SKBitmap? Bitmap, List<CanvasActionViewModel>? CanvasActions)> RestoreSnapshotAsync(string snapshotId)
    {
        if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
        {
            return (null, null);
        }

        SKBitmap? bitmap = null;
        List<CanvasActionViewModel>? actions = null;

        if (File.Exists(snapshot.BitmapFilePath))
        {
            await Task.Run(() =>
            {
                using var stream = File.OpenRead(snapshot.BitmapFilePath);
                using var codec = SKCodec.Create(stream);
                var info = new SKImageInfo
                {
                    AlphaType = SKAlphaType.Unpremul,
                    ColorSpace = codec.Info.ColorSpace,
                    ColorType = codec.Info.ColorType,
                    Height = codec.Info.Height,
                    Width = codec.Info.Width,
                };
                bitmap = SKBitmap.Decode(codec, info);
            });
        }

        if (snapshot.ActionsFilePath != null && File.Exists(snapshot.ActionsFilePath))
        {
            string json = await File.ReadAllTextAsync(snapshot.ActionsFilePath);
            var wrapper = JsonSerializer.Deserialize<MaskViewModel>(json, _jsonOptions);
            
            if (wrapper != null)
            {
                actions = new List<CanvasActionViewModel>();
                if (wrapper.Lines != null)
                {
                    actions.AddRange(wrapper.Lines);
                }
                if (wrapper.SegmentationMasks != null)
                {
                    actions.AddRange(wrapper.SegmentationMasks);
                }
            }
        }

        await DeleteSnapshotAsync(snapshotId);

        return (bitmap, actions);
    }

    public Task DeleteSnapshotAsync(string snapshotId)
    {
        if (_snapshots.Remove(snapshotId, out var snapshot))
        {
            _insertionOrder.Remove(snapshotId);

            if (File.Exists(snapshot.BitmapFilePath))
            {
                File.Delete(snapshot.BitmapFilePath);
            }

            if (snapshot.ActionsFilePath != null && File.Exists(snapshot.ActionsFilePath))
            {
                File.Delete(snapshot.ActionsFilePath);
            }
        }

        return Task.CompletedTask;
    }

    public Task ClearAllAsync()
    {
        _snapshots.Clear();
        _insertionOrder.Clear();

        if (Directory.Exists(_snapshotDirectory))
        {
            Directory.Delete(_snapshotDirectory, true);
        }

        Directory.CreateDirectory(_snapshotDirectory);

        return Task.CompletedTask;
    }

    private async Task enforceSnapshotLimitAsync()
    {
        while (_snapshots.Count > MaxSnapshots && _insertionOrder.Count > 0)
        {
            var oldestId = _insertionOrder[0];
            await DeleteSnapshotAsync(oldestId);
        }
    }
}
