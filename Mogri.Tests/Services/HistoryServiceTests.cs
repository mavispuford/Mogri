using LiteDB;
using Moq;
using Mogri.Interfaces.Services;
using Mogri.Models;
using Mogri.Services;
using Xunit;

namespace Mogri.Tests.Services;

public class HistoryServiceTests
{
    [Fact]
    public async Task SearchAsync_EmptyQuery_FiltersByHiddenState()
    {
        // Arrange
        using var fixture = CreateFixture();
        var visibleItem = CreateEntity("visible.png", "visible prompt", false);
        var hiddenItem = CreateEntity("hidden.png", "hidden prompt", true);
        fixture.Insert(visibleItem, hiddenItem);

        // Act
        var visibleResults = (await fixture.Service.SearchAsync(string.Empty, 0, 10)).ToList();
        var hiddenResults = (await fixture.Service.SearchAsync(string.Empty, 0, 10, isHidden: true)).ToList();

        // Assert
        Assert.Single(visibleResults);
        Assert.Equal(visibleItem.ImageFileName, visibleResults[0].ImageFileName);
        Assert.False(visibleResults[0].IsHidden);
        Assert.Single(hiddenResults);
        Assert.Equal(hiddenItem.ImageFileName, hiddenResults[0].ImageFileName);
        Assert.True(hiddenResults[0].IsHidden);
    }

    [Fact]
    public async Task SearchAsync_TextQuery_FiltersByHiddenState()
    {
        // Arrange
        using var fixture = CreateFixture();
        var visibleItem = CreateEntity("visible.png", "red cat", false);
        var hiddenItem = CreateEntity("hidden.png", "red cat", true);
        var unrelatedHiddenItem = CreateEntity("unrelated.png", "blue dog", true);
        fixture.Insert(visibleItem, hiddenItem, unrelatedHiddenItem);

        // Act
        var visibleResults = (await fixture.Service.SearchAsync("cat", 0, 10)).ToList();
        var hiddenResults = (await fixture.Service.SearchAsync("cat", 0, 10, isHidden: true)).ToList();

        // Assert
        Assert.Single(visibleResults);
        Assert.Equal(visibleItem.ImageFileName, visibleResults[0].ImageFileName);
        Assert.Single(hiddenResults);
        Assert.Equal(hiddenItem.ImageFileName, hiddenResults[0].ImageFileName);
    }

    [Fact]
    public async Task SearchAsync_LegacyItemWithoutHiddenField_IsIncludedAsVisible()
    {
        // Arrange
        using var fixture = CreateFixture();
        fixture.InsertLegacy("legacy.png", "legacy prompt");
        fixture.EnsureHiddenIndex();

        // Act
        var visibleResults = (await fixture.Service.SearchAsync(string.Empty, 0, 10)).ToList();
        var textResults = (await fixture.Service.SearchAsync("legacy", 0, 10)).ToList();
        var hiddenResults = (await fixture.Service.SearchAsync(string.Empty, 0, 10, isHidden: true)).ToList();

        // Assert
        Assert.Single(visibleResults);
        Assert.Equal("legacy.png", visibleResults[0].ImageFileName);
        Assert.Single(textResults);
        Assert.Equal("legacy.png", textResults[0].ImageFileName);
        Assert.Empty(hiddenResults);
    }

    [Fact]
    public async Task SetItemsHiddenAsync_BatchUpdatesVisibility()
    {
        // Arrange
        using var fixture = CreateFixture();
        var firstItem = CreateEntity("first.png", "first prompt", false);
        var secondItem = CreateEntity("second.png", "second prompt", false);
        fixture.Insert(firstItem, secondItem);

        // Act
        await fixture.Service.SetItemsHiddenAsync([firstItem, secondItem], true);

        // Assert
        var visibleResults = (await fixture.Service.SearchAsync(string.Empty, 0, 10)).ToList();
        var hiddenResults = (await fixture.Service.SearchAsync(string.Empty, 0, 10, isHidden: true)).ToList();
        Assert.Empty(visibleResults);
        Assert.Equal(2, hiddenResults.Count);
        Assert.All(hiddenResults, item => Assert.True(item.IsHidden));
    }

    [Fact]
    public async Task SetItemsHiddenAsync_WithEmptyOrMissingItems_LeavesStoredItemsUnchanged()
    {
        // Arrange
        using var fixture = CreateFixture();
        var existingItem = CreateEntity("existing.png", "existing prompt", false);
        var missingItem = CreateEntity("missing.png", "missing prompt", false);
        fixture.Insert(existingItem);

        // Act
        await fixture.Service.SetItemsHiddenAsync(Array.Empty<HistoryEntity>(), true);
        await fixture.Service.SetItemsHiddenAsync([missingItem], true);

        // Assert
        var visibleResults = (await fixture.Service.SearchAsync(string.Empty, 0, 10)).ToList();
        var hiddenResults = (await fixture.Service.SearchAsync(string.Empty, 0, 10, isHidden: true)).ToList();
        Assert.Single(visibleResults);
        Assert.Equal(existingItem.ImageFileName, visibleResults[0].ImageFileName);
        Assert.Empty(hiddenResults);
    }

    private static HistoryServiceFixture CreateFixture()
    {
        return new HistoryServiceFixture();
    }

    private static HistoryEntity CreateEntity(string fileName, string prompt, bool isHidden)
    {
        return new HistoryEntity
        {
            Id = ObjectId.NewObjectId(),
            ImageFileName = fileName,
            ThumbnailFileName = $"thumbnail-{fileName}",
            UserPrompt = prompt,
            CreatedAt = DateTime.UtcNow,
            IsHidden = isHidden
        };
    }

    private sealed class HistoryServiceFixture : IDisposable
    {
        private const string CollectionName = "history";
        private readonly string databaseDirectory;

        public HistoryServiceFixture()
        {
            databaseDirectory = Path.Combine(Path.GetTempPath(), $"mogri-history-{Guid.NewGuid():N}");
            Directory.CreateDirectory(databaseDirectory);
            var databasePath = Path.Combine(databaseDirectory, "history.db");
            Service = new HistoryService(new Mock<IFileService>().Object, databasePath);
        }

        public HistoryService Service { get; }

        public void Insert(params HistoryEntity[] items)
        {
            using var database = new LiteDatabase(Path.Combine(databaseDirectory, "history.db"));
            var collection = database.GetCollection<HistoryEntity>(CollectionName);
            collection.InsertBulk(items);
        }

        public void InsertLegacy(string fileName, string prompt)
        {
            using var database = new LiteDatabase(Path.Combine(databaseDirectory, "history.db"));
            var collection = database.GetCollection(CollectionName);
            collection.Insert(new BsonDocument
            {
                ["_id"] = ObjectId.NewObjectId(),
                [nameof(HistoryEntity.ImageFileName)] = fileName,
                [nameof(HistoryEntity.ThumbnailFileName)] = $"thumbnail-{fileName}",
                [nameof(HistoryEntity.UserPrompt)] = prompt,
                [nameof(HistoryEntity.CreatedAt)] = DateTime.UtcNow
            });
        }

        public void EnsureHiddenIndex()
        {
            using var database = new LiteDatabase(Path.Combine(databaseDirectory, "history.db"));
            var collection = database.GetCollection<HistoryEntity>(CollectionName);
            collection.EnsureIndex(x => x.IsHidden);
        }

        public void Dispose()
        {
            if (Directory.Exists(databaseDirectory))
            {
                Directory.Delete(databaseDirectory, recursive: true);
            }
        }
    }
}
