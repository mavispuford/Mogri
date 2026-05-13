using Moq;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.ViewModels;
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
    public async Task DeleteSelectedItems_WhenMoreHistoryExists_RefillsRemovedSlots()
    {
        // Arrange
        _ = Application.Current ?? new Application();

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
        var initialEntities = Enumerable.Range(1, 15).Select(index => CreateHistoryEntity($"history-{index}.png")).ToList();
        var refillEntity = CreateHistoryEntity("history-16.png");
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
            .Setup(service => service.SearchAsync(string.Empty, 0, 15))
            .ReturnsAsync(initialEntities);
        historyService
            .Setup(service => service.SearchAsync(string.Empty, 14, 1))
            .ReturnsAsync(new List<HistoryEntity> { refillEntity });
        mainThreadService
            .Setup(service => service.InvokeOnMainThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(async action =>
            {
                await action();
                invoked.TrySetResult(true);
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
            mainThreadService.Object,
            navigationService.Object,
            loadingCoordinator.Object);

        await viewModel.OnNavigatedToAsync();
        await invoked.Task.WaitAsync(TimeSpan.FromSeconds(2));

        viewModel.SelectedItems = new List<object> { viewModel.HistoryItems[0] };

        // Act
        await viewModel.DeleteSelectedItemsCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(15, viewModel.HistoryItems.Count);
        historyService.Verify(service => service.SearchAsync(string.Empty, 14, 1), Times.Once);
    }

    [Fact]
    public async Task DeleteSelectedItems_WhenDeletingTrailingBufferedItem_OnlyRefillsRemovedSlots()
    {
        // Arrange
        _ = Application.Current ?? new Application();

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
        var initialEntities = Enumerable.Range(1, 15).Select(index => CreateHistoryEntity($"history-{index}.png")).ToList();
        var refillEntity = CreateHistoryEntity("history-16.png");
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
            .Setup(service => service.SearchAsync(string.Empty, 0, 15))
            .ReturnsAsync(initialEntities);
        historyService
            .Setup(service => service.SearchAsync(string.Empty, 14, 1))
            .ReturnsAsync(new List<HistoryEntity> { refillEntity });
        mainThreadService
            .Setup(service => service.InvokeOnMainThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(async action =>
            {
                await action();
                invoked.TrySetResult(true);
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
            mainThreadService.Object,
            navigationService.Object,
            loadingCoordinator.Object);

        await viewModel.OnNavigatedToAsync();
        await invoked.Task.WaitAsync(TimeSpan.FromSeconds(2));

        viewModel.SelectedItems = new List<object> { viewModel.HistoryItems[^1] };

        // Act
        await viewModel.DeleteSelectedItemsCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(15, viewModel.HistoryItems.Count);
        historyService.Verify(service => service.SearchAsync(string.Empty, 14, 1), Times.Once);
    }

    [Fact]
    public async Task OnNavigatedTo_WhenHistoryChangesExist_UsesMainThreadService()
    {
        // Arrange
        _ = Application.Current ?? new Application();

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
            .Setup(service => service.SearchAsync(string.Empty, 0, 15))
            .ReturnsAsync(new List<Mogri.Models.HistoryEntity>());
        mainThreadService
            .Setup(service => service.InvokeOnMainThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(async action =>
            {
                await action();
                invoked.TrySetResult(true);
            });

        var viewModel = new HistoryPageViewModel(
            fileService.Object,
            imageService.Object,
            historyService.Object,
            serviceProvider.Object,
            popupService.Object,
            toastService.Object,
            mainThreadService.Object,
            navigationService.Object,
            loadingCoordinator.Object);

        // Act
        await viewModel.OnNavigatedToAsync();
        await invoked.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        mainThreadService.Verify(service => service.InvokeOnMainThreadAsync(It.IsAny<Func<Task>>()), Times.Once);
        historyService.Verify(service => service.SearchAsync(string.Empty, 0, 15), Times.Once);
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