using Mogri.Helpers;
using Xunit;

namespace Mogri.Tests.Helpers;

public class ModelResourceHelperTests
{
    [Fact]
    public void FindMatch_WithPunctuationDifferences_ReturnsMatchingResource()
    {
        // Arrange
        var resources = new[]
        {
            "Qwen3-VL-4B-Instruct\\Qwen3-VL-4B-Instruct_quanto_bf16_int8.safetensors"
        };

        // Act
        var result = ModelResourceHelper.FindMatch(resources, "qwen3vl_4b");

        // Assert
        Assert.Equal(resources[0], result);
    }

    [Fact]
    public void FindMatch_WithSpecificFamilyToken_SkipsBroaderFamily()
    {
        // Arrange
        var resources = new[]
        {
            "Qwen3-VL-32B-Instruct\\Qwen3-VL-32B-Instruct-layer50_bf16.safetensors",
            "Qwen3-VL-4B-Instruct\\Qwen3-VL-4B-Instruct_quanto_bf16_int8.safetensors"
        };

        // Act
        var result = ModelResourceHelper.FindMatch(resources, "qwen3vl_4b");

        // Assert
        Assert.Equal(resources[1], result);
    }

    [Fact]
    public void FindMatch_WithNoMatchingResource_ReturnsNull()
    {
        // Arrange
        var resources = new[] { "ae.safetensors", "qwen_image_vae.safetensors" };

        // Act
        var result = ModelResourceHelper.FindMatch(resources, "qwen3vl_4b");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("Qwen3-VL-4B-Instruct\\Qwen3-VL-4B-Instruct_quanto_bf16_int8.safetensors", true)]
    [InlineData("Qwen3-VL-4B-Instruct\\Qwen3-VL-4B-Instruct_vision_bf16.safetensors", false)]
    [InlineData("Krea2Turbo_quanto_bf16_int8.safetensors", false)]
    public void IsKreaTextEncoder_ReturnsExpectedResult(string resource, bool expectedResult)
    {
        // Arrange

        // Act
        var result = ModelResourceHelper.IsKreaTextEncoder(resource);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("qwen_3_4b.safetensors", true)]
    [InlineData("Qwen3-VL-4B-Instruct\\Qwen3-VL-4B-Instruct_quanto_bf16_int8.safetensors", false)]
    public void IsZImageTextEncoder_ReturnsExpectedResult(string resource, bool expectedResult)
    {
        // Arrange

        // Act
        var result = ModelResourceHelper.IsZImageTextEncoder(resource);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("t5xxl_fp16.safetensors", true)]
    [InlineData("clip_l.safetensors", false)]
    public void IsFluxT5TextEncoder_ReturnsExpectedResult(string resource, bool expectedResult)
    {
        // Arrange

        // Act
        var result = ModelResourceHelper.IsFluxT5TextEncoder(resource);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("clip_l.safetensors", true)]
    [InlineData("t5xxl_fp16.safetensors", false)]
    public void IsFluxClipLTextEncoder_ReturnsExpectedResult(string resource, bool expectedResult)
    {
        // Arrange

        // Act
        var result = ModelResourceHelper.IsFluxClipLTextEncoder(resource);

        // Assert
        Assert.Equal(expectedResult, result);
    }
}