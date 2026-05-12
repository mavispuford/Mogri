using Moq;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.ViewModels;
using Mogri.ViewModels.Pages;
using Xunit;

namespace Mogri.Tests.ViewModels;

public class ViewModelNavigationTests
{
    [Fact]
    public void OnBackButtonPressed_UsesNavigationServiceGoBackAsync()
    {
        // Arrange
        var loadingCoordinator = new Mock<ILoadingCoordinator>();
        var navigationService = CreateNavigationService();
        var viewModel = new TestPageViewModel(loadingCoordinator.Object, navigationService.Object);

        // Act
        var handled = viewModel.OnBackButtonPressed();

        // Assert
        Assert.False(handled);
        navigationService.Verify(service => service.GoBackAsync(), Times.Once);
    }

    [Fact]
    public async Task NavigateToLicensesPage_UsesNavigationServiceGoToAsync()
    {
        // Arrange
        var loadingCoordinator = new Mock<ILoadingCoordinator>();
        var toastService = new Mock<IToastService>();
        var navigationService = CreateNavigationService();
        var viewModel = new AboutPageViewModel(
            loadingCoordinator.Object,
            toastService.Object,
            navigationService.Object);

        // Act
        await viewModel.NavigateToLicensesPageCommand.ExecuteAsync(null);

        // Assert
        navigationService.Verify(service => service.GoToAsync("LicensesPage"), Times.Once);
    }

    [Fact]
    public async Task ItemTapped_WithNavigationPayload_UsesNavigationServiceGoBackAsync()
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
        var popupResult = new Dictionary<string, object>
        {
            { NavigationParams.PromptSettings, new PromptSettings() }
        };

        popupService
            .Setup(service => service.ShowPopupForResultAsync("HistoryItemPopup", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(popupResult);

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
            HistoryItems = new System.Collections.ObjectModel.ObservableCollection<IHistoryItemViewModel> { historyItem.Object }
        };

        // Act
        await viewModel.ItemTappedCommand.ExecuteAsync(historyItem.Object);

        // Assert
        navigationService.Verify(
            service => service.GoBackAsync(It.Is<IDictionary<string, object>>(parameters => ReferenceEquals(parameters, popupResult))),
            Times.Once);
    }

    private static Mock<IHistoryItemViewModel> CreateHistoryItem(string fileName)
    {
        var historyItem = new Mock<IHistoryItemViewModel>();
        historyItem.SetupProperty(item => item.FileName, fileName);
        historyItem.SetupProperty(item => item.ThumbnailFileName, $"thumb-{fileName}");
        historyItem.SetupProperty(item => item.Entity, new HistoryEntity
        {
            ImageFileName = fileName,
            ThumbnailFileName = $"thumb-{fileName}"
        });
        return historyItem;
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

    private sealed class TestPageViewModel : PageViewModel
    {
        public TestPageViewModel(ILoadingCoordinator loadingCoordinator, INavigationService navigationService)
            : base(loadingCoordinator, navigationService)
        {
        }
    }
}