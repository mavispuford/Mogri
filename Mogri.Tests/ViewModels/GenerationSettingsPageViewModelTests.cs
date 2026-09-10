using Moq;
using Mogri.Enums;
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

    [Theory]
    [InlineData(ModelType.SD15)]
    [InlineData(ModelType.SDXL)]
    public async Task OnNavigatedToAsync_IntegratedCheckpointModel_HidesResourceSelectors(ModelType modelType)
    {
        // Arrange
        var viewModel = CreateViewModel(
            new PromptSettings { ModelType = modelType },
            new BackendCapabilities
            {
                SupportsVaes = true,
                SupportsTextEncoders = true
            });

        // Act
        await viewModel.OnNavigatedToAsync();

        // Assert
        Assert.False(viewModel.IsVaeVisible);
        Assert.False(viewModel.IsTextEncoderVisible);
    }

    [Fact]
    public async Task OnNavigatedToAsync_ZImageTurbo_ShowsResourceSelectors()
    {
        // Arrange
        var viewModel = CreateViewModel(
            new PromptSettings { ModelType = ModelType.ZImageTurbo },
            new BackendCapabilities
            {
                SupportsVaes = true,
                SupportsTextEncoders = true
            });

        // Act
        await viewModel.OnNavigatedToAsync();

        // Assert
        Assert.True(viewModel.IsVaeVisible);
        Assert.True(viewModel.IsTextEncoderVisible);
    }

    [Fact]
    public async Task SelectedModelTypeChanged_ToZImageTurbo_SelectsZImageModel()
    {
        // Arrange
        var viewModel = CreateViewModel(
            new PromptSettings
            {
                Model = CreateModel("v1-5-pruned-emaonly.safetensors")
            },
            BackendCapabilities.None,
            [
                CreateModel("v1-5-pruned-emaonly.safetensors"),
                CreateModel("z_image_turbo_bf16.safetensors")
            ]);
        await viewModel.OnNavigatedToAsync();

        // Act
        viewModel.SelectedModelType = ModelType.ZImageTurbo;

        // Assert
        Assert.Equal("z_image_turbo_bf16.safetensors", viewModel.Model?.Key);
    }

    [Fact]
    public async Task SelectedModelTypeChanged_ToZImageTurboWithoutMatchingModel_ClearsStaleModel()
    {
        // Arrange
        var viewModel = CreateViewModel(
            new PromptSettings
            {
                Model = CreateModel("v1-5-pruned-emaonly.safetensors")
            },
            BackendCapabilities.None,
            [CreateModel("v1-5-pruned-emaonly.safetensors")]);
        await viewModel.OnNavigatedToAsync();

        // Act
        viewModel.SelectedModelType = ModelType.ZImageTurbo;

        // Assert
        Assert.Null(viewModel.Model);
    }

    private static GenerationSettingsPageViewModel CreateViewModel(
        PromptSettings settings,
        BackendCapabilities capabilities,
        List<IModelViewModel>? models = null)
    {
        var backend = new Mock<IImageGenerationCoordinator>();
        backend.SetupGet(service => service.Capabilities).Returns(capabilities);
        backend.Setup(service => service.GetSamplersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        backend.Setup(service => service.GetUpscalersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IUpscalerViewModel>());
        backend.Setup(service => service.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(models ?? new List<IModelViewModel>());
        backend.Setup(service => service.GetSchedulersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
        backend.Setup(service => service.GetVaesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "ae.safetensors" });
        backend.Setup(service => service.GetTextEncodersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "qwen_3_4b.safetensors" });

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

    private static IModelViewModel CreateModel(string key)
    {
        var model = new Mock<IModelViewModel>();
        model.SetupProperty(value => value.Key, key);
        model.SetupProperty(value => value.DisplayName, key);
        return model.Object;
    }
}