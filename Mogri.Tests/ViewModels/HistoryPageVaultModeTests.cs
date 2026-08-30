using Moq;
using Mogri.Enums;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.ViewModels;
using Xunit;

namespace Mogri.Tests.ViewModels;

public class HistoryPageVaultModeTests
{
    [Fact]
    public async Task PullDownGesture_FirstPull_ShowsToastAndClickHaptic()
    {
        // Arrange
        var context = CreateContext();

        // Act
        await context.ViewModel.PullDownGestureCommand.ExecuteAsync(null);

        // Assert
        Assert.False(context.ViewModel.IsVaultMode);
        context.HapticsService.Verify(service => service.Perform(HapticType.Click), Times.Once);
        context.HapticsService.Verify(service => service.Perform(HapticType.LongPress), Times.Never);
        context.ToastService.Verify(service => service.ShowAsync("Swipe down again to open vault"), Times.Once);
    }

    [Fact]
    public async Task PullDownGesture_SecondPullWithinThreeSeconds_EntersVaultAfterRefreshEnds()
    {
        // Arrange
        var context = CreateContext();

        // Act
        await context.ViewModel.PullDownGestureCommand.ExecuteAsync(null);
        await context.ViewModel.PullDownGestureCommand.ExecuteAsync(null);

        // Assert
        Assert.False(context.ViewModel.IsVaultMode);
        context.HapticsService.Verify(service => service.Perform(HapticType.Click), Times.Once);
        context.HapticsService.Verify(service => service.Perform(HapticType.LongPress), Times.Once);
        context.ToastService.Verify(service => service.ShowAsync("Swipe down again to open vault"), Times.Once);

        // Act
        await context.ViewModel.RefreshEndedCommand.ExecuteAsync(null);

        // Assert
        Assert.True(context.ViewModel.IsVaultMode);
    }

    [Fact]
    public async Task PullDownGesture_SecondPull_DefersHistoryReloadUntilRefreshEnds()
    {
        // Arrange
        var loadCompleted = CreateCompletionSource();
        var context = CreateContext(loadCompleted);
        context.HistoryService
            .Setup(service => service.InitializeAsync())
            .ReturnsAsync(false);
        context.HistoryService
            .Setup(service => service.SearchAsync(string.Empty, 0, 48, false))
            .ReturnsAsync(Array.Empty<HistoryEntity>());
        context.HistoryService
            .Setup(service => service.SearchAsync(string.Empty, 0, 48, true))
            .ReturnsAsync(Array.Empty<HistoryEntity>());

        await context.ViewModel.OnNavigatedToAsync();
        await loadCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Act
        await context.ViewModel.PullDownGestureCommand.ExecuteAsync(null);
        await context.ViewModel.PullDownGestureCommand.ExecuteAsync(null);

        // Assert
        context.HistoryService.Verify(service => service.SearchAsync(string.Empty, 0, 48, true), Times.Never);

        // Act
        await context.ViewModel.RefreshEndedCommand.ExecuteAsync(null);

        // Assert
        context.HistoryService.Verify(service => service.SearchAsync(string.Empty, 0, 48, true), Times.Once);
    }

    [Fact]
    public async Task PullDownGesture_AfterWindowSeconds_RestartsTimerBeforeEnteringVault()
    {
        // Arrange
        var context = CreateContext();

        // Act
        await context.ViewModel.PullDownGestureCommand.ExecuteAsync(null);
        await Task.Delay(TimeSpan.FromSeconds(2.2));
        await context.ViewModel.PullDownGestureCommand.ExecuteAsync(null);

        // Assert
        Assert.False(context.ViewModel.IsVaultMode);
        context.HapticsService.Verify(service => service.Perform(HapticType.Click), Times.Exactly(2));
        context.HapticsService.Verify(service => service.Perform(HapticType.LongPress), Times.Never);
        context.ToastService.Verify(service => service.ShowAsync("Swipe down again to open vault"), Times.Exactly(2));

        // Act
        await context.ViewModel.PullDownGestureCommand.ExecuteAsync(null);

        // Assert
        Assert.False(context.ViewModel.IsVaultMode);
        context.HapticsService.Verify(service => service.Perform(HapticType.LongPress), Times.Once);

        // Act
        await context.ViewModel.RefreshEndedCommand.ExecuteAsync(null);

        // Assert
        Assert.True(context.ViewModel.IsVaultMode);
    }

    [Fact]
    public async Task PullDownGesture_WhenSelectionModeIsEnabled_IgnoresGesture()
    {
        // Arrange
        var context = CreateContext();
        context.ViewModel.SelectionModeEnabled = true;

        // Act
        await context.ViewModel.PullDownGestureCommand.ExecuteAsync(null);

        // Assert
        Assert.False(context.ViewModel.IsVaultMode);
        context.HapticsService.Verify(service => service.Perform(It.IsAny<HapticType>()), Times.Never);
        context.ToastService.Verify(service => service.ShowAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PullDownGesture_LeavesRefreshActiveUntilTouchEnds()
    {
        // Arrange
        var context = CreateContext();

        // Act
        await context.ViewModel.PullDownGestureCommand.ExecuteAsync(null);

        // Assert
        Assert.True(context.ViewModel.IsRefreshing);

        // Act
        await context.ViewModel.RefreshEndedCommand.ExecuteAsync(null);

        // Assert
        Assert.False(context.ViewModel.IsRefreshing);
    }

    [Fact]
    public async Task PullDownGesture_InVaultMode_ExitsVaultModeAfterRefreshEnds()
    {
        // Arrange
        var context = CreateContext();
        context.ViewModel.IsVaultMode = true;

        // Act
        await context.ViewModel.PullDownGestureCommand.ExecuteAsync(null);

        // Assert
        Assert.True(context.ViewModel.IsVaultMode);
        context.HapticsService.Verify(service => service.Perform(HapticType.Click), Times.Once);
        context.ToastService.Verify(service => service.ShowAsync(It.IsAny<string>()), Times.Never);

        // Act
        await context.ViewModel.RefreshEndedCommand.ExecuteAsync(null);

        // Assert
        Assert.False(context.ViewModel.IsVaultMode);
    }

    [Fact]
    public async Task NavigationLifecycle_WhenEnteringOrLeavingHistory_ResetsVaultMode()
    {
        // Arrange
        var loadCompleted = CreateCompletionSource();
        var context = CreateContext(loadCompleted);
        context.HistoryService
            .Setup(service => service.InitializeAsync())
            .ReturnsAsync(false);
        context.HistoryService
            .Setup(service => service.SearchAsync(string.Empty, 0, 48, false))
            .ReturnsAsync(Array.Empty<HistoryEntity>());
        context.ViewModel.IsVaultMode = true;

        // Act
        await context.ViewModel.OnNavigatedFromAsync();

        // Assert
        Assert.False(context.ViewModel.IsVaultMode);

        // Arrange
        context.ViewModel.IsVaultMode = true;

        // Act
        await context.ViewModel.OnNavigatedToAsync();
        await loadCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.False(context.ViewModel.IsVaultMode);
        context.HistoryService.Verify(service => service.SearchAsync(string.Empty, 0, 48, false), Times.Once);
    }

    [Fact]
    public async Task SearchText_InVaultMode_SearchesHiddenItems()
    {
        // Arrange
        var loadCompleted = CreateCompletionSource();
        var searchCompleted = CreateCompletionSource();
        var context = CreateContext(loadCompleted);
        context.HistoryService
            .Setup(service => service.InitializeAsync())
            .ReturnsAsync(false);
        context.HistoryService
            .Setup(service => service.SearchAsync(string.Empty, 0, 48, false))
            .ReturnsAsync(Array.Empty<HistoryEntity>());
        context.HistoryService
            .Setup(service => service.SearchAsync("hidden", 0, 48, true))
            .Callback(() => searchCompleted.TrySetResult(true))
            .ReturnsAsync(Array.Empty<HistoryEntity>());

        // Act
        await context.ViewModel.OnNavigatedToAsync();
        await loadCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        context.ViewModel.IsVaultMode = true;
        context.ViewModel.SearchText = "hidden";
        await searchCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        context.HistoryService.Verify(service => service.SearchAsync("hidden", 0, 48, true), Times.Once);
    }

    [Fact]
    public async Task HideSelectedItems_MovesBatchAndRefillsHistory()
    {
        // Arrange
        var loadCompleted = CreateCompletionSource();
        var context = CreateContext(loadCompleted);
        var firstEntity = CreateHistoryEntity("first.png");
        var secondEntity = CreateHistoryEntity("second.png");
        var replacementEntity = CreateHistoryEntity("replacement.png");
        var firstItem = CreateHistoryItem(firstEntity);
        var secondItem = CreateHistoryItem(secondEntity);
        var replacementItem = CreateHistoryItem(replacementEntity);
        var historyItems = new Queue<Mock<IHistoryItemViewModel>>(
            [firstItem, secondItem, replacementItem]);

        context.HistoryService
            .Setup(service => service.InitializeAsync())
            .ReturnsAsync(false);
        context.HistoryService
            .Setup(service => service.SearchAsync(string.Empty, 0, 48, false))
            .ReturnsAsync([firstEntity, secondEntity]);
        context.HistoryService
            .Setup(service => service.SearchAsync(string.Empty, 1, 1, false))
            .ReturnsAsync([replacementEntity]);
        context.HistoryService
            .Setup(service => service.SetItemsHiddenAsync(It.IsAny<IEnumerable<HistoryEntity>>(), true))
            .Returns(Task.CompletedTask);
        context.ServiceProvider
            .Setup(provider => provider.GetService(typeof(IHistoryItemViewModel)))
            .Returns(() => historyItems.Dequeue().Object);

        // Act
        await context.ViewModel.OnNavigatedToAsync();
        await loadCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        context.ViewModel.SelectionModeEnabled = true;
        context.ViewModel.SelectedItems = new List<object> { firstItem.Object };
        await context.ViewModel.HideSelectedItemsCommand.ExecuteAsync(null);

        // Assert
        context.HistoryService.Verify(
            service => service.SetItemsHiddenAsync(
                It.Is<IEnumerable<HistoryEntity>>(items => items.Single() == firstEntity),
                true),
            Times.Once);
        context.HistoryService.Verify(service => service.SearchAsync(string.Empty, 1, 1, false), Times.Once);
        Assert.DoesNotContain(firstItem.Object, context.ViewModel.HistoryItems);
        Assert.Contains(secondItem.Object, context.ViewModel.HistoryItems);
        Assert.Contains(replacementItem.Object, context.ViewModel.HistoryItems);
        Assert.False(context.ViewModel.SelectionModeEnabled);
        context.ToastService.Verify(service => service.ShowAsync("1 item(s) moved to vault"), Times.Once);
    }

    [Fact]
    public async Task UnhideSelectedItems_RestoresBatchAndRefillsVault()
    {
        // Arrange
        var loadCompleted = CreateCompletionSource();
        var context = CreateContext(loadCompleted);
        var hiddenEntity = CreateHistoryEntity("hidden.png", true);
        var replacementEntity = CreateHistoryEntity("hidden-replacement.png", true);
        var hiddenItem = CreateHistoryItem(hiddenEntity);
        var replacementItem = CreateHistoryItem(replacementEntity);
        var historyItems = new Queue<Mock<IHistoryItemViewModel>>(
            [hiddenItem, replacementItem]);

        context.HistoryService
            .Setup(service => service.InitializeAsync())
            .ReturnsAsync(false);
        context.HistoryService
            .Setup(service => service.SearchAsync(string.Empty, 0, 48, false))
            .ReturnsAsync(Array.Empty<HistoryEntity>());
        context.HistoryService
            .Setup(service => service.SearchAsync(string.Empty, 0, 48, true))
            .ReturnsAsync([hiddenEntity]);
        context.HistoryService
            .Setup(service => service.SearchAsync(string.Empty, 0, 1, true))
            .ReturnsAsync([replacementEntity]);
        context.HistoryService
            .Setup(service => service.SetItemsHiddenAsync(It.IsAny<IEnumerable<HistoryEntity>>(), false))
            .Returns(Task.CompletedTask);
        context.ServiceProvider
            .Setup(provider => provider.GetService(typeof(IHistoryItemViewModel)))
            .Returns(() => historyItems.Dequeue().Object);

        // Act
        await context.ViewModel.OnNavigatedToAsync();
        await loadCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        context.ViewModel.IsVaultMode = true;
        await context.ViewModel.LoadItemsCommand.ExecuteAsync(null);
        context.ViewModel.SelectionModeEnabled = true;
        context.ViewModel.SelectedItems = new List<object> { hiddenItem.Object };
        await context.ViewModel.UnhideSelectedItemsCommand.ExecuteAsync(null);

        // Assert
        context.HistoryService.Verify(
            service => service.SetItemsHiddenAsync(
                It.Is<IEnumerable<HistoryEntity>>(items => items.Single() == hiddenEntity),
                false),
            Times.Once);
        context.HistoryService.Verify(service => service.SearchAsync(string.Empty, 0, 1, true), Times.Once);
        Assert.DoesNotContain(hiddenItem.Object, context.ViewModel.HistoryItems);
        Assert.Contains(replacementItem.Object, context.ViewModel.HistoryItems);
        Assert.False(context.ViewModel.SelectionModeEnabled);
        context.ToastService.Verify(service => service.ShowAsync("1 item(s) restored to history"), Times.Once);
    }

    private static TestContext CreateContext(TaskCompletionSource<bool>? mainThreadInvocation = null)
    {
        var fileService = new Mock<IFileService>();
        var imageService = new Mock<IImageService>();
        var historyService = new Mock<IHistoryService>();
        var serviceProvider = new Mock<IServiceProvider>();
        var popupService = new Mock<IPopupService>();
        var toastService = new Mock<IToastService>();
        var hapticsService = new Mock<IHapticsService>();
        var mainThreadService = CreateMainThreadService(mainThreadInvocation);
        var navigationService = new Mock<INavigationService>();
        var loadingCoordinator = new Mock<ILoadingCoordinator>();
        var viewModel = new HistoryPageViewModel(
            fileService.Object,
            imageService.Object,
            historyService.Object,
            serviceProvider.Object,
            popupService.Object,
            toastService.Object,
            hapticsService.Object,
            mainThreadService.Object,
            navigationService.Object,
            loadingCoordinator.Object);

        return new TestContext(
            viewModel,
            fileService,
            imageService,
            historyService,
            serviceProvider,
            popupService,
            toastService,
            hapticsService,
            mainThreadService,
            navigationService,
            loadingCoordinator);
    }

    private static Mock<IMainThreadService> CreateMainThreadService(TaskCompletionSource<bool>? invocationSignal)
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
            .Returns<Func<Task>>(async action =>
            {
                try
                {
                    await action();
                    invocationSignal?.TrySetResult(true);
                }
                catch (Exception exception)
                {
                    invocationSignal?.TrySetException(exception);
                    throw;
                }
            });
        return mainThreadService;
    }

    private static TaskCompletionSource<bool> CreateCompletionSource()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static Mock<IHistoryItemViewModel> CreateHistoryItem(HistoryEntity entity)
    {
        var historyItem = new Mock<IHistoryItemViewModel>();
        historyItem.SetupProperty(item => item.FileName, entity.ImageFileName);
        historyItem.SetupProperty(item => item.ThumbnailFileName, entity.ThumbnailFileName);
        historyItem.SetupProperty(item => item.Entity, entity);
        historyItem.SetupProperty(item => item.IsSelected);
        historyItem
            .Setup(item => item.InitWith(It.IsAny<HistoryEntity>(), It.IsAny<IFileService>(), It.IsAny<IImageService>()))
            .Returns<HistoryEntity, IFileService, IImageService>((loadedEntity, _, _) =>
            {
                historyItem.Object.Entity = loadedEntity;
                historyItem.Object.FileName = loadedEntity.ImageFileName;
                historyItem.Object.ThumbnailFileName = loadedEntity.ThumbnailFileName;
                return Task.CompletedTask;
            });
        return historyItem;
    }

    private static HistoryEntity CreateHistoryEntity(string fileName, bool isHidden = false)
    {
        return new HistoryEntity
        {
            ImageFileName = fileName,
            ThumbnailFileName = $"thumb-{fileName}",
            IsHidden = isHidden
        };
    }

    private sealed record TestContext(
        HistoryPageViewModel ViewModel,
        Mock<IFileService> FileService,
        Mock<IImageService> ImageService,
        Mock<IHistoryService> HistoryService,
        Mock<IServiceProvider> ServiceProvider,
        Mock<IPopupService> PopupService,
        Mock<IToastService> ToastService,
        Mock<IHapticsService> HapticsService,
        Mock<IMainThreadService> MainThreadService,
        Mock<INavigationService> NavigationService,
        Mock<ILoadingCoordinator> LoadingCoordinator);
}
