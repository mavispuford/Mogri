using Moq;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.ViewModels;
using Xunit;

namespace Mogri.Tests.ViewModels;

public class GenerationSettingsPageViewModelTests
{
    [Fact]
    public async Task OnNavigatedToAsync_ForgeWithoutSourceImage_ShowsHiresFixSteps()
    {
        // Arrange
        var viewModel = CreateViewModel(
            new PromptSettings(),
            new BackendCapabilities
            {
                SupportsUpscaling = true,
                SupportsHiresFix = true
            });

        // Act
        await viewModel.OnNavigatedToAsync();

        // Assert
        Assert.True(viewModel.IsHiresFixStepsVisible);
    }

    [Fact]
    public async Task OnNavigatedToAsync_ForgeWithSourceImage_HidesHiresFixSteps()
    {
        // Arrange
        var viewModel = CreateViewModel(
            new PromptSettings { InitImage = "source-image" },
            new BackendCapabilities
            {
                SupportsUpscaling = true,
                SupportsHiresFix = true
            });

        // Act
        await viewModel.OnNavigatedToAsync();

        // Assert
        Assert.False(viewModel.IsHiresFixStepsVisible);
    }

    [Fact]
    public async Task OnNavigatedToAsync_BackendWithoutHiresFix_HidesHiresFixSteps()
    {
        // Arrange
        var viewModel = CreateViewModel(
            new PromptSettings(),
            new BackendCapabilities { SupportsUpscaling = false });

        // Act
        await viewModel.OnNavigatedToAsync();

        // Assert
        Assert.False(viewModel.IsHiresFixStepsVisible);
    }

    private static GenerationSettingsPageViewModel CreateViewModel(
        PromptSettings settings,
        BackendCapabilities capabilities)
    {
        var backend = new Mock<IImageGenerationCoordinator>();
        backend.SetupGet(service => service.Capabilities).Returns(capabilities);
        backend.Setup(service => service.GetSamplersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        backend.Setup(service => service.GetUpscalersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IUpscalerViewModel>());
        backend.Setup(service => service.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IModelViewModel>());
        backend.Setup(service => service.GetSchedulersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var popupService = new Mock<IPopupService>();
        var navigationService = new Mock<INavigationService>();
        var loadingCoordinator = new Mock<ILoadingCoordinator>();
        var presetService = new Mock<IPresetService>();
        presetService.Setup(service => service.GetPresetsAsync())
            .ReturnsAsync(new List<string>());

        var viewModel = new GenerationSettingsPageViewModel(
            backend.Object,
            popupService.Object,
            navigationService.Object,
            loadingCoordinator.Object,
            presetService.Object);

        viewModel.ApplyQueryAttributes(new Dictionary<string, object>
        {
            { NavigationParams.PromptSettings, settings }
        });

        return viewModel;
    }
}