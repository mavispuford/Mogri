using Moq;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Models;
using Mogri.ViewModels;
using Xunit;

namespace Mogri.Tests.ViewModels;

public class MainPageViewModelAnimationTests
{
    [Fact]
    public async Task OnNavigatedToAsync_WhenGenerationProgressChanged_UsesAnimationServiceToUpdateProgress()
    {
        // Arrange
        const float progress = 0.42f;

        var fileService = new Mock<IFileService>();
        var imageGenerationCoordinator = new Mock<IImageGenerationCoordinator>();
        var generationTaskCoordinator = new Mock<IGenerationTaskCoordinator>();
        var serviceProvider = new Mock<IServiceProvider>();
        var imageService = new Mock<IImageService>();
        var popupService = new Mock<IPopupService>();
        var checkpointSettingsService = new Mock<ICheckpointSettingsService>();
        var animationService = new Mock<IAnimationService>();
        var toastService = new Mock<IToastService>();
        var hapticsService = new Mock<IHapticsService>();
        var navigationService = CreateNavigationService();
        var loadingCoordinator = new Mock<ILoadingCoordinator>();

        imageGenerationCoordinator.SetupGet(coordinator => coordinator.Initialized).Returns(true);
        generationTaskCoordinator.SetupGet(coordinator => coordinator.IsRunning).Returns(false);
        generationTaskCoordinator.SetupGet(coordinator => coordinator.LastResult).Returns((GenerationTaskResult?)null);
        animationService
            .Setup(service => service.AnimateProgress(0f, progress, It.IsAny<Action<float>>()))
            .Callback<float, float, Action<float>>((_, end, onUpdate) => onUpdate(end));

        var viewModel = new MainPageViewModel(
            fileService.Object,
            imageGenerationCoordinator.Object,
            generationTaskCoordinator.Object,
            serviceProvider.Object,
            imageService.Object,
            popupService.Object,
            checkpointSettingsService.Object,
            animationService.Object,
            toastService.Object,
            hapticsService.Object,
            navigationService.Object,
            loadingCoordinator.Object);

        // Act
        await viewModel.OnNavigatedToAsync();
        generationTaskCoordinator.Raise(coordinator => coordinator.ProgressChanged += null, generationTaskCoordinator.Object, progress);

        // Assert
        animationService.Verify(service => service.AnimateProgress(0f, progress, It.IsAny<Action<float>>()), Times.Once);
        Assert.Equal(progress, viewModel.Progress);
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