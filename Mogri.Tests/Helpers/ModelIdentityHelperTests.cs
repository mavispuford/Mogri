using Mogri.Helpers;
using Xunit;

namespace Mogri.Tests.Helpers;

public class ModelIdentityHelperTests
{
    [Theory]
    [InlineData(null, "Krea2Turbo.safetensors", "Krea2Turbo.safetensors")]
    [InlineData("", "Krea2Turbo.safetensors", "Krea2Turbo.safetensors")]
    [InlineData("  ", "Krea2Turbo.safetensors", "Krea2Turbo.safetensors")]
    public void GetDisplayName_MissingModelName_UsesModelKey(string? modelName, string modelKey, string expectedDisplayName)
    {
        // Arrange

        // Act
        var result = ModelIdentityHelper.GetDisplayName(modelName, modelKey);

        // Assert
        Assert.Equal(expectedDisplayName, result);
    }

    [Fact]
    public void GetDisplayName_WithModelName_PreservesModelName()
    {
        // Arrange
        const string modelName = "Krea 2 Turbo";

        // Act
        var result = ModelIdentityHelper.GetDisplayName(modelName, "Krea2Turbo.safetensors");

        // Assert
        Assert.Equal(modelName, result);
    }

    [Theory]
    [InlineData("Krea2Turbo.safetensors", "Krea2Turbo.safetensors", true)]
    [InlineData("Krea 2 Turbo", "Krea 2 Turbo", true)]
    [InlineData("Krea2Turbo.safetensors", "OtherModel.safetensors", false)]
    public void AreEquivalent_WithModelIdentities_ReturnsExpectedResult(
        string firstIdentity,
        string secondIdentity,
        bool expectedResult)
    {
        // Arrange

        // Act
        var result = ModelIdentityHelper.AreEquivalent(
            firstIdentity,
            firstIdentity,
            secondIdentity,
            secondIdentity);

        // Assert
        Assert.Equal(expectedResult, result);
    }
}