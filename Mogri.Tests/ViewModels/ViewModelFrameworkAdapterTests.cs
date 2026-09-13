using Moq;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.ViewModels;
using SkiaSharp;
using Xunit;

namespace Mogri.Tests.ViewModels;

public class ViewModelFrameworkAdapterTests
{
    [Fact]
    public async Task Save_WithResultItem_WritesFileAndShowsToast()
    {
        // Arrange
        var popupService = new Mock<IPopupService>();
        var fileService = new Mock<IFileService>();
        var toastService = new Mock<IToastService>();
        var resultItem = CreateResultItem("result.png");
        var stream = new MemoryStream([1, 2, 3]);

        fileService
            .Setup(service => service.GetFileStreamFromInternalStorageAsync("result.png"))
            .ReturnsAsync(stream);

        var viewModel = new ResultItemPopupViewModel(
            popupService.Object,
            fileService.Object,
            toastService.Object)
        {
            ResultItem = resultItem.Object
        };

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        fileService.Verify(
            service => service.WriteImageFileToExternalStorageAsync(
                "result.png",
                It.IsAny<Stream>(),
                true),
            Times.Once);
        toastService.Verify(service => service.ShowAsync("Image saved."), Times.Once);
    }

    [Fact]
    public async Task Save_WithHistoryItem_WritesFileAndShowsToast()
    {
        // Arrange
        var popupService = new Mock<IPopupService>();
        var fileService = new Mock<IFileService>();
        var historyService = new Mock<IHistoryService>();
        var imageService = new Mock<IImageService>();
        var imageGenerationService = new Mock<IImageGenerationCoordinator>();
        var toastService = new Mock<IToastService>();
        var mainThreadService = CreateMainThreadService();
        var historyItem = CreateHistoryItem("history.png");
        var stream = new MemoryStream([4, 5, 6]);

        fileService
            .Setup(service => service.GetFileStreamFromInternalStorageAsync("history.png"))
            .ReturnsAsync(stream);

        var viewModel = new HistoryItemPopupViewModel(
            popupService.Object,
            fileService.Object,
            historyService.Object,
            imageService.Object,
            imageGenerationService.Object,
            toastService.Object,
            mainThreadService.Object)
        {
            HistoryItem = historyItem.Object
        };

        // Act
        await viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        fileService.Verify(
            service => service.WriteImageFileToExternalStorageAsync(
                "history.png",
                It.IsAny<Stream>(),
                true),
            Times.Once);
        toastService.Verify(service => service.ShowAsync("Image saved."), Times.Once);
    }

    [Fact]
    public async Task ImageInfo_WithUpscaledImage_ShowsActualResolution()
    {
        // Arrange
        var popupService = new Mock<IPopupService>();
        var fileService = new Mock<IFileService>();
        var historyService = new Mock<IHistoryService>();
        var imageService = new Mock<IImageService>();
        var imageGenerationService = new Mock<IImageGenerationCoordinator>();
        var toastService = new Mock<IToastService>();
        var mainThreadService = CreateMainThreadService();
        var historyItem = CreateHistoryItem("history.png");
        historyItem.SetupProperty(item => item.Settings, new PromptSettings
        {
            Width = 1024,
            Height = 1024,
            ActualWidth = 2048,
            ActualHeight = 2048
        });

        popupService
            .Setup(service => service.DisplayAlertAsync("Image Info", It.IsAny<string>(), "Copy to clipboard", "Close"))
            .ReturnsAsync(false);

        var viewModel = new HistoryItemPopupViewModel(
            popupService.Object,
            fileService.Object,
            historyService.Object,
            imageService.Object,
            imageGenerationService.Object,
            toastService.Object,
            mainThreadService.Object)
        {
            HistoryItem = historyItem.Object
        };

        // Act
        await viewModel.ImageInfoCommand.ExecuteAsync(null);

        // Assert
        popupService.Verify(
            service => service.DisplayAlertAsync(
                "Image Info",
                It.Is<string>(message => message.Contains("Size: 1024x1024 (Actual: 2048x2048)", StringComparison.Ordinal)),
                "Copy to clipboard",
                "Close"),
            Times.Once);
    }

    [Fact]
    public async Task ImageInfo_WithNativeUpscaler_OmitsUnsupportedScaleSettings()
    {
        // Arrange
        var popupService = new Mock<IPopupService>();
        var fileService = new Mock<IFileService>();
        var historyService = new Mock<IHistoryService>();
        var imageService = new Mock<IImageService>();
        var imageGenerationService = new Mock<IImageGenerationCoordinator>();
        var toastService = new Mock<IToastService>();
        var mainThreadService = CreateMainThreadService();
        var historyItem = CreateHistoryItem("history.png");
        historyItem.SetupProperty(item => item.Settings, new PromptSettings
        {
            EnableUpscaling = true,
            Upscaler = "4x-UltraSharp.pth",
            UpscaleLevel = 0,
            UpscaleSteps = 0
        });

        popupService
            .Setup(service => service.DisplayAlertAsync("Image Info", It.IsAny<string>(), "Copy to clipboard", "Close"))
            .ReturnsAsync(false);

        var viewModel = new HistoryItemPopupViewModel(
            popupService.Object,
            fileService.Object,
            historyService.Object,
            imageService.Object,
            imageGenerationService.Object,
            toastService.Object,
            mainThreadService.Object)
        {
            HistoryItem = historyItem.Object
        };

        // Act
        await viewModel.ImageInfoCommand.ExecuteAsync(null);

        // Assert
        popupService.Verify(
            service => service.DisplayAlertAsync(
                "Image Info",
                It.Is<string>(message =>
                    message.Contains("Upscaler: 4x-UltraSharp.pth", StringComparison.Ordinal) &&
                    !message.Contains("Upscale Level:", StringComparison.Ordinal) &&
                    !message.Contains("Upscale Steps:", StringComparison.Ordinal)),
                "Copy to clipboard",
                "Close"),
            Times.Once);
    }

    [Fact]
    public async Task HistoryItemLoad_UsesStreamImageInfoPath()
    {
        // Arrange
        var popupService = new Mock<IPopupService>();
        var fileService = new Mock<IFileService>();
        var historyService = new Mock<IHistoryService>();
        var imageService = new Mock<IImageService>();
        var imageGenerationService = new Mock<IImageGenerationCoordinator>();
        var toastService = new Mock<IToastService>();
        var mainThreadService = new Mock<IMainThreadService>();
        var settingsLoaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var historyItem = CreateHistoryItem("history.png");
        historyItem.SetupProperty(item => item.Settings);

        fileService
            .Setup(service => service.GetFileStreamFromInternalStorageAsync("history.png"))
            .ReturnsAsync(() => new MemoryStream([1, 2, 3]));
        imageService
            .Setup(service => service.GetSkBitmapFromStream(It.IsAny<Stream>()))
            .Returns((SKBitmap?)null);
        imageGenerationService
            .Setup(service => service.GetImageInfoAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptSettings());
        mainThreadService
            .Setup(service => service.InvokeOnMainThreadAsync(It.IsAny<Action>()))
            .Returns<Action>(action =>
            {
                action();
                if (historyItem.Object.Settings != null)
                {
                    settingsLoaded.TrySetResult(true);
                }

                return Task.CompletedTask;
            });

        var viewModel = new HistoryItemPopupViewModel(
            popupService.Object,
            fileService.Object,
            historyService.Object,
            imageService.Object,
            imageGenerationService.Object,
            toastService.Object,
            mainThreadService.Object)
        {
            HistoryItem = historyItem.Object
        };

        // Act
        await settingsLoaded.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        imageGenerationService.Verify(
            service => service.GetImageInfoAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Once);
        imageGenerationService.Verify(
            service => service.GetImageInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ImageInfo_WithoutActualResolution_UsesDimensionsFromDisplayLoad()
    {
        // Arrange
        var popupService = new Mock<IPopupService>();
        var fileService = new Mock<IFileService>();
        var historyService = new Mock<IHistoryService>();
        var imageService = new Mock<IImageService>();
        var imageGenerationService = new Mock<IImageGenerationCoordinator>();
        var toastService = new Mock<IToastService>();
        var mainThreadService = CreateMainThreadService();
        var historyItem = CreateHistoryItem("history.png");
        var settings = new PromptSettings
        {
            Width = 1024,
            Height = 1024
        };
        var originalBitmap = new SKBitmap(3000, 1500);
        var displayBitmap = new SKBitmap(2048, 1024);
        historyItem.SetupProperty(item => item.Settings, settings);

        fileService
            .Setup(service => service.GetFileStreamFromInternalStorageAsync("history.png"))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));
        imageService
            .Setup(service => service.GetSkBitmapFromStream(It.IsAny<Stream>()))
            .Returns(originalBitmap);
        imageService
            .Setup(service => service.GetResizedSKBitmap(It.IsAny<SKBitmap>(), 2048, 2048, true, true))
            .Returns(displayBitmap);
        popupService
            .Setup(service => service.DisplayAlertAsync("Image Info", It.IsAny<string>(), "Copy to clipboard", "Close"))
            .ReturnsAsync(false);

        var viewModel = new HistoryItemPopupViewModel(
            popupService.Object,
            fileService.Object,
            historyService.Object,
            imageService.Object,
            imageGenerationService.Object,
            toastService.Object,
            mainThreadService.Object)
        {
            HistoryItem = historyItem.Object
        };

        // Act
        await viewModel.ImageInfoCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(3000, settings.ActualWidth);
        Assert.Equal(1500, settings.ActualHeight);
        imageService.Verify(service => service.GetSkBitmapFromStream(It.IsAny<Stream>()), Times.Once);
        popupService.Verify(
            service => service.DisplayAlertAsync(
                "Image Info",
                It.Is<string>(message => message.Contains("Size: 1024x1024 (Actual: 3000x1500)", StringComparison.Ordinal)),
                "Copy to clipboard",
                "Close"),
            Times.Once);

        displayBitmap.Dispose();
    }

    [Fact]
    public async Task Delete_WithHistoryItem_DeletesViaHistoryService()
    {
        // Arrange
        var popupService = new Mock<IPopupService>();
        var fileService = new Mock<IFileService>();
        var historyService = new Mock<IHistoryService>();
        var imageService = new Mock<IImageService>();
        var imageGenerationService = new Mock<IImageGenerationCoordinator>();
        var toastService = new Mock<IToastService>();
        var mainThreadService = CreateMainThreadService();
        var historyItem = CreateHistoryItem("history.png");

        popupService
            .Setup(service => service.DisplayAlertAsync("Confirm", "Are you sure you would like to delete this image?", "DELETE", "Cancel"))
            .ReturnsAsync(true);
        popupService
            .Setup(service => service.ClosePopupAsync(It.IsAny<Mogri.Interfaces.ViewModels.Popups.IPopupBaseViewModel>(), It.IsAny<object?>()))
            .Returns(Task.CompletedTask);

        var viewModel = new HistoryItemPopupViewModel(
            popupService.Object,
            fileService.Object,
            historyService.Object,
            imageService.Object,
            imageGenerationService.Object,
            toastService.Object,
            mainThreadService.Object)
        {
            HistoryItem = historyItem.Object
        };

        // Act
        await viewModel.DeleteCommand.ExecuteAsync(null);

        // Assert
        historyService.Verify(
            service => service.DeleteItemsAsync(It.Is<IList<HistoryEntity>>(items =>
                items.Count == 1 &&
                ReferenceEquals(items[0], historyItem.Object.Entity))),
            Times.Once);
        fileService.Verify(service => service.DeleteFileFromInternalStorageAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteSelectedItems_WhenDeleteFails_ShowsErrorToast()
    {
        // Arrange
        var fileService = new Mock<IFileService>();
        var imageService = new Mock<IImageService>();
        var historyService = new Mock<IHistoryService>();
        var serviceProvider = new Mock<IServiceProvider>();
        var popupService = new Mock<IPopupService>();
        var toastService = new Mock<IToastService>();
        var mainThreadService = CreateMainThreadService();
        var navigationService = CreateNavigationService();
        var loadingCoordinator = new Mock<ILoadingCoordinator>();
        var historyItem = CreateHistoryItem("history.png");

        popupService
            .Setup(service => service.DisplayAlertAsync("Confirm", "Delete 1 item?", "DELETE", "Cancel"))
            .ReturnsAsync(true);
        historyService
            .Setup(service => service.DeleteItemsAsync(It.IsAny<IList<Mogri.Models.HistoryEntity>>()))
            .ThrowsAsync(new InvalidOperationException("disk full"));

        var viewModel = new HistoryPageViewModel(
            fileService.Object,
            imageService.Object,
            historyService.Object,
            serviceProvider.Object,
            popupService.Object,
            toastService.Object,
            new Mock<IHapticsService>().Object,
            mainThreadService.Object,
            navigationService.Object,
            loadingCoordinator.Object)
        {
            SelectedItems = new List<object> { historyItem.Object }
        };

        // Act
        await viewModel.DeleteSelectedItemsCommand.ExecuteAsync(null);

        // Assert
        toastService.Verify(service => service.ShowAsync("Failed to delete items: disk full"), Times.Once);
    }

    [Fact]
    public void SelectionChanged_UpdatesHistoryItemSelectionState()
    {
        // Arrange
        var fileService = new Mock<IFileService>();
        var imageService = new Mock<IImageService>();
        var historyService = new Mock<IHistoryService>();
        var serviceProvider = new Mock<IServiceProvider>();
        var popupService = new Mock<IPopupService>();
        var toastService = new Mock<IToastService>();
        var mainThreadService = CreateMainThreadService();
        var navigationService = CreateNavigationService();
        var loadingCoordinator = new Mock<ILoadingCoordinator>();
        var firstItem = CreateHistoryItem("first.png");
        var secondItem = CreateHistoryItem("second.png");

        var viewModel = new HistoryPageViewModel(
            fileService.Object,
            imageService.Object,
            historyService.Object,
            serviceProvider.Object,
            popupService.Object,
            toastService.Object,
            new Mock<IHapticsService>().Object,
            mainThreadService.Object,
            navigationService.Object,
            loadingCoordinator.Object)
        {
            SelectedItems = new List<object>()
        };
        viewModel.HistoryItems.Add(firstItem.Object);
        viewModel.HistoryItems.Add(secondItem.Object);

        // Act
        viewModel.SelectedItems.Add(firstItem.Object);
        viewModel.SelectionChangedCommand.Execute(null);

        // Assert
        Assert.True(firstItem.Object.IsSelected);
        Assert.False(secondItem.Object.IsSelected);
    }

    [Fact]
    public async Task SelectAllResults_WithUnloadedMatches_SelectsAllResults()
    {
        // Arrange
        var fileService = new Mock<IFileService>();
        var imageService = new Mock<IImageService>();
        var historyService = new Mock<IHistoryService>();
        var serviceProvider = new Mock<IServiceProvider>();
        var popupService = new Mock<IPopupService>();
        var toastService = new Mock<IToastService>();
        var mainThreadService = CreateMainThreadService();
        var navigationService = CreateNavigationService();
        var loadingCoordinator = new Mock<ILoadingCoordinator>();
        var firstItem = CreateHistoryItem("tree-1.png");
        var secondItem = CreateHistoryItem("tree-2.png");
        var matchingEntities = new List<HistoryEntity>
        {
            firstItem.Object.Entity,
            secondItem.Object.Entity,
            CreateHistoryEntity("tree-3.png")
        };

        historyService
            .Setup(service => service.SearchAsync("tree", 0, int.MaxValue))
            .ReturnsAsync(matchingEntities);

        var viewModel = new HistoryPageViewModel(
            fileService.Object,
            imageService.Object,
            historyService.Object,
            serviceProvider.Object,
            popupService.Object,
            toastService.Object,
            new Mock<IHapticsService>().Object,
            mainThreadService.Object,
            navigationService.Object,
            loadingCoordinator.Object)
        {
            SearchText = "tree",
            SelectedItems = new List<object>()
        };
        viewModel.HistoryItems.Add(firstItem.Object);
        viewModel.HistoryItems.Add(secondItem.Object);

        // Act
        await viewModel.SelectAllResultsCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("3 items selected", viewModel.SelectedItemsText);
        Assert.True(firstItem.Object.IsSelected);
        Assert.True(secondItem.Object.IsSelected);
        Assert.Equal(2, viewModel.SelectedItems.Count);
    }

    [Fact]
    public async Task DeleteSelectedItems_WithSearchQuery_DeletesOnlyMatchingResults()
    {
        // Arrange
        var fileService = new Mock<IFileService>();
        var imageService = new Mock<IImageService>();
        var historyService = new Mock<IHistoryService>();
        var serviceProvider = new Mock<IServiceProvider>();
        var popupService = new Mock<IPopupService>();
        var toastService = new Mock<IToastService>();
        var mainThreadService = CreateMainThreadService();
        var navigationService = CreateNavigationService();
        var loadingCoordinator = new Mock<ILoadingCoordinator>();
        var matchingItem = CreateHistoryItem("tree.png");
        var nonMatchingItem = CreateHistoryItem("car.png");
        var matchingEntities = new List<HistoryEntity>
        {
            matchingItem.Object.Entity,
            CreateHistoryEntity("tree-2.png")
        };

        historyService
            .Setup(service => service.SearchAsync("tree", 0, int.MaxValue))
            .ReturnsAsync(matchingEntities);
        historyService
            .Setup(service => service.DeleteItemsAsync(It.IsAny<IList<HistoryEntity>>()))
            .Returns(Task.CompletedTask);
        popupService
            .Setup(service => service.DisplayAlertAsync("Confirm", "Delete 2 items?", "DELETE", "Cancel"))
            .ReturnsAsync(true);

        var viewModel = new HistoryPageViewModel(
            fileService.Object,
            imageService.Object,
            historyService.Object,
            serviceProvider.Object,
            popupService.Object,
            toastService.Object,
            new Mock<IHapticsService>().Object,
            mainThreadService.Object,
            navigationService.Object,
            loadingCoordinator.Object)
        {
            SearchText = "tree",
            SelectedItems = new List<object>()
        };
        viewModel.HistoryItems.Add(matchingItem.Object);
        viewModel.HistoryItems.Add(nonMatchingItem.Object);

        await viewModel.SelectAllResultsCommand.ExecuteAsync(null);

        // Act
        await viewModel.DeleteSelectedItemsCommand.ExecuteAsync(null);

        // Assert
        historyService.Verify(
            service => service.DeleteItemsAsync(It.Is<IList<HistoryEntity>>(items =>
                items.Count == 2 &&
                items.All(item => item.ImageFileName.StartsWith("tree", StringComparison.Ordinal)))),
            Times.Once);
        Assert.False(nonMatchingItem.Object.IsSelected);
    }

    [Fact]
    public async Task DeleteSelectedItems_WhenMoreHistoryExists_RefillsRemovedSlots()
    {
        // Arrange
        var fileService = new Mock<IFileService>();
        var imageService = new Mock<IImageService>();
        var historyService = new Mock<IHistoryService>();
        var serviceProvider = new Mock<IServiceProvider>();
        var popupService = new Mock<IPopupService>();
        var toastService = new Mock<IToastService>();
        var loadingCoordinator = new Mock<ILoadingCoordinator>();
        var invoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mainThreadService = new Mock<IMainThreadService>();
        var navigationService = CreateNavigationService();
        var initialEntities = Enumerable.Range(1, 48).Select(index => CreateHistoryEntity($"history-{index}.png")).ToList();
        var refillEntity = CreateHistoryEntity("history-49.png");
        var historyItems = new Queue<Mock<IHistoryItemViewModel>>(
            initialEntities
                .Append(refillEntity)
                .Select(entity => CreateHistoryItem(entity.ImageFileName)));

        popupService
            .Setup(service => service.DisplayAlertAsync("Confirm", "Delete 1 item?", "DELETE", "Cancel"))
            .ReturnsAsync(true);
        historyService
            .Setup(service => service.InitializeAsync())
            .ReturnsAsync(true);
        historyService
            .Setup(service => service.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((string _, int skip, int take, bool _) =>
                (IList<HistoryEntity>)((skip, take) switch
                {
                    (0, 48) => initialEntities,
                    (47, 1) => new List<HistoryEntity> { refillEntity },
                    _ => new List<HistoryEntity>()
                }));
        mainThreadService
            .Setup(service => service.InvokeOnMainThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(async action =>
            {
                try
                {
                    await action();
                    invoked.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    invoked.TrySetException(ex);
                    throw;
                }
            });
        serviceProvider
            .Setup(provider => provider.GetService(typeof(IHistoryItemViewModel)))
            .Returns(() => historyItems.Dequeue().Object);

        var viewModel = new HistoryPageViewModel(
            fileService.Object,
            imageService.Object,
            historyService.Object,
            serviceProvider.Object,
            popupService.Object,
            toastService.Object,
            new Mock<IHapticsService>().Object,
            mainThreadService.Object,
            navigationService.Object,
            loadingCoordinator.Object);

        await viewModel.OnNavigatedToAsync();
        await invoked.Task.WaitAsync(TimeSpan.FromSeconds(2));

        viewModel.SelectedItems = new List<object> { viewModel.HistoryItems[0] };

        // Act
        await viewModel.DeleteSelectedItemsCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(48, viewModel.HistoryItems.Count);
        historyService.Verify(service => service.SearchAsync(string.Empty, 47, 1), Times.Once);
    }

    [Fact]
    public async Task DeleteSelectedItems_WhenDeletingTrailingBufferedItem_OnlyRefillsRemovedSlots()
    {
        // Arrange
        var fileService = new Mock<IFileService>();
        var imageService = new Mock<IImageService>();
        var historyService = new Mock<IHistoryService>();
        var serviceProvider = new Mock<IServiceProvider>();
        var popupService = new Mock<IPopupService>();
        var toastService = new Mock<IToastService>();
        var loadingCoordinator = new Mock<ILoadingCoordinator>();
        var invoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mainThreadService = new Mock<IMainThreadService>();
        var navigationService = CreateNavigationService();
        var initialEntities = Enumerable.Range(1, 48).Select(index => CreateHistoryEntity($"history-{index}.png")).ToList();
        var refillEntity = CreateHistoryEntity("history-49.png");
        var historyItems = new Queue<Mock<IHistoryItemViewModel>>(
            initialEntities
                .Append(refillEntity)
                .Select(entity => CreateHistoryItem(entity.ImageFileName)));

        popupService
            .Setup(service => service.DisplayAlertAsync("Confirm", "Delete 1 item?", "DELETE", "Cancel"))
            .ReturnsAsync(true);
        historyService
            .Setup(service => service.InitializeAsync())
            .ReturnsAsync(true);
        historyService
            .Setup(service => service.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((string _, int skip, int take, bool _) =>
                (IList<HistoryEntity>)((skip, take) switch
                {
                    (0, 48) => initialEntities,
                    (47, 1) => new List<HistoryEntity> { refillEntity },
                    _ => new List<HistoryEntity>()
                }));
        mainThreadService
            .Setup(service => service.InvokeOnMainThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(async action =>
            {
                try
                {
                    await action();
                    invoked.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    invoked.TrySetException(ex);
                    throw;
                }
            });
        serviceProvider
            .Setup(provider => provider.GetService(typeof(IHistoryItemViewModel)))
            .Returns(() => historyItems.Dequeue().Object);

        var viewModel = new HistoryPageViewModel(
            fileService.Object,
            imageService.Object,
            historyService.Object,
            serviceProvider.Object,
            popupService.Object,
            toastService.Object,
            new Mock<IHapticsService>().Object,
            mainThreadService.Object,
            navigationService.Object,
            loadingCoordinator.Object);

        await viewModel.OnNavigatedToAsync();
        await invoked.Task.WaitAsync(TimeSpan.FromSeconds(2));

        viewModel.SelectedItems = new List<object> { viewModel.HistoryItems[^1] };

        // Act
        await viewModel.DeleteSelectedItemsCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(48, viewModel.HistoryItems.Count);
        historyService.Verify(service => service.SearchAsync(string.Empty, 47, 1), Times.Once);
    }

    [Fact]
    public async Task OnNavigatedTo_WhenHistoryChangesExist_UsesMainThreadService()
    {
        // Arrange
        var fileService = new Mock<IFileService>();
        var imageService = new Mock<IImageService>();
        var historyService = new Mock<IHistoryService>();
        var serviceProvider = new Mock<IServiceProvider>();
        var popupService = new Mock<IPopupService>();
        var toastService = new Mock<IToastService>();
        var loadingCoordinator = new Mock<ILoadingCoordinator>();
        var invoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mainThreadService = new Mock<IMainThreadService>();
        var navigationService = CreateNavigationService();

        historyService
            .Setup(service => service.InitializeAsync())
            .ReturnsAsync(true);
        historyService
            .Setup(service => service.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((string _, int skip, int take, bool _) =>
                (IList<Mogri.Models.HistoryEntity>)((skip, take) switch
                {
                    (0, 48) => new List<Mogri.Models.HistoryEntity>(),
                    _ => new List<Mogri.Models.HistoryEntity>()
                }));
        mainThreadService
            .Setup(service => service.InvokeOnMainThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(async action =>
            {
                try
                {
                    await action();
                    invoked.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    invoked.TrySetException(ex);
                    throw;
                }
            });

        var viewModel = new HistoryPageViewModel(
            fileService.Object,
            imageService.Object,
            historyService.Object,
            serviceProvider.Object,
            popupService.Object,
            toastService.Object,
            new Mock<IHapticsService>().Object,
            mainThreadService.Object,
            navigationService.Object,
            loadingCoordinator.Object);

        // Act
        await viewModel.OnNavigatedToAsync();
        await invoked.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        mainThreadService.Verify(service => service.InvokeOnMainThreadAsync(It.IsAny<Func<Task>>()), Times.Once);
        historyService.Verify(service => service.SearchAsync(string.Empty, 0, 48), Times.Once);
    }

    private static Mock<IResultItemViewModel> CreateResultItem(string internalUri)
    {
        var resultItem = new Mock<IResultItemViewModel>();
        resultItem.SetupProperty(item => item.InternalUri, internalUri);
        return resultItem;
    }

    private static Mock<IHistoryItemViewModel> CreateHistoryItem(string fileName)
    {
        var historyItem = new Mock<IHistoryItemViewModel>();
        historyItem.SetupProperty(item => item.FileName, fileName);
        historyItem.SetupProperty(item => item.ThumbnailFileName, $"thumb-{fileName}");
        historyItem.SetupProperty(item => item.Entity, CreateHistoryEntity(fileName));
        historyItem.SetupProperty(item => item.IsSelected);
        historyItem
            .Setup(item => item.InitWith(It.IsAny<HistoryEntity>(), It.IsAny<IFileService>(), It.IsAny<IImageService>()))
            .Returns<HistoryEntity, IFileService, IImageService>((entity, _, _) =>
            {
                historyItem.Object.Entity = entity;
                historyItem.Object.FileName = entity.ImageFileName;
                historyItem.Object.ThumbnailFileName = entity.ThumbnailFileName;
                return Task.CompletedTask;
            });
        return historyItem;
    }

    private static HistoryEntity CreateHistoryEntity(string fileName)
    {
        return new HistoryEntity
        {
            ImageFileName = fileName,
            ThumbnailFileName = $"thumb-{fileName}"
        };
    }

    private static Mock<IMainThreadService> CreateMainThreadService()
    {
        var mainThreadService = new Mock<IMainThreadService>();
        mainThreadService
            .Setup(service => service.InvokeOnMainThreadAsync(It.IsAny<Action>()))
            .Returns<Action>(action =>
            {
                action();
                return Task.CompletedTask;
            });
        mainThreadService
            .Setup(service => service.InvokeOnMainThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(action => action());
        return mainThreadService;
    }

    private static Mock<INavigationService> CreateNavigationService()
    {
        var navigationService = new Mock<INavigationService>();
        navigationService
            .Setup(service => service.GoBackAsync())
            .Returns(Task.CompletedTask);
        navigationService
            .Setup(service => service.GoBackAsync(It.IsAny<IDictionary<string, object>>()))
            .Returns(Task.CompletedTask);
        navigationService
            .Setup(service => service.GoToAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        navigationService
            .Setup(service => service.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .Returns(Task.CompletedTask);
        navigationService
            .Setup(service => service.PopToRootAsync())
            .Returns(Task.CompletedTask);
        navigationService
            .Setup(service => service.PopToRootAndGoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .Returns(Task.CompletedTask);
        return navigationService;
    }
}