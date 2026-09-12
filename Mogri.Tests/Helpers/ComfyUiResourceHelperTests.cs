using Mogri.Helpers;
using Xunit;

namespace Mogri.Tests.Helpers;

public class ComfyUiResourceHelperTests
{
    [Fact]
    public void ParseUpscalerModelNames_WithComboOptions_ReturnsNamesInServerOrder()
    {
        // Arrange
        var objectInfoJson = """
            {
              "UpscaleModelLoader": {
                "input": {
                  "required": {
                    "model_name": [
                      "COMBO",
                      {
                        "multiselect": false,
                        "options": [
                          "4x-UltraSharp.pth",
                          "4x_NMKD-Siax_200k.pth",
                          "4x-AnimeSharp.pth",
                          "4x_foolhardy_Remacri.pth",
                          "4x_RealisticRescaler_100000_G.pth",
                          "8x_NMKD-Superscale_150000_G.pth",
                          "ESRGAN_4x.pth",
                          "RealESRGAN_x4plus.pth",
                          "RealESRGAN_x4plus_anime_6B.pth"
                        ]
                      }
                    ]
                  }
                }
              }
            }
            """;

        // Act
        var result = ComfyUiResourceHelper.ParseUpscalerModelNames(objectInfoJson);

        // Assert
        Assert.Equal(
        [
            "4x-UltraSharp.pth",
            "4x_NMKD-Siax_200k.pth",
            "4x-AnimeSharp.pth",
            "4x_foolhardy_Remacri.pth",
            "4x_RealisticRescaler_100000_G.pth",
            "8x_NMKD-Superscale_150000_G.pth",
            "ESRGAN_4x.pth",
            "RealESRGAN_x4plus.pth",
            "RealESRGAN_x4plus_anime_6B.pth"
        ], result);
    }

    [Fact]
    public void ParseUpscalerModelNames_WithMissingNode_ReturnsEmpty()
    {
        // Arrange
        var objectInfoJson = "{}";

        // Act
        var result = ComfyUiResourceHelper.ParseUpscalerModelNames(objectInfoJson);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseUpscalerModelNames_WithMalformedJson_ReturnsEmpty()
    {
        // Arrange
        var objectInfoJson = "{\"UpscaleModelLoader\":";

        // Act
        var result = ComfyUiResourceHelper.ParseUpscalerModelNames(objectInfoJson);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseUpscalerModelNames_WithMalformedModelName_ReturnsEmpty()
    {
        // Arrange
        var objectInfoJson = """
            {
              "UpscaleModelLoader": {
                "input": {
                  "required": {
                    "model_name": ["COMBO", {"options": "not-an-array"}]
                  }
                }
              }
            }
            """;

        // Act
        var result = ComfyUiResourceHelper.ParseUpscalerModelNames(objectInfoJson);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseUpscalerModelNames_WithBlankAndDuplicateOptions_PreservesFirstValidNames()
    {
        // Arrange
        var objectInfoJson = """
            {
              "UpscaleModelLoader": {
                "input": {
                  "required": {
                    "model_name": [
                      "COMBO",
                      {
                        "options": ["first.pth", " ", "FIRST.PTH", "", null, "second.pth"]
                      }
                    ]
                  }
                }
              }
            }
            """;

        // Act
        var result = ComfyUiResourceHelper.ParseUpscalerModelNames(objectInfoJson);

        // Assert
        Assert.Equal(["first.pth", "second.pth"], result);
    }

    [Fact]
    public void ParseUpscalerModelNames_WithDirectStringArray_UsesFallback()
    {
        // Arrange
        var objectInfoJson = """
            {
              "UpscaleModelLoader": {
                "input": {
                  "required": {
                    "model_name": ["first.pth", "second.pth", "first.pth"]
                  }
                }
              }
            }
            """;

        // Act
        var result = ComfyUiResourceHelper.ParseUpscalerModelNames(objectInfoJson);

        // Assert
        Assert.Equal(["first.pth", "second.pth"], result);
    }

    [Fact]
    public void FindUpscalerModelName_WithCaseDifference_ReturnsServerName()
    {
        // Arrange
        var availableNames = new[] { "4x-UltraSharp.pth", "RealESRGAN_x4plus.pth" };

        // Act
        var result = ComfyUiResourceHelper.FindUpscalerModelName(availableNames, "4X-ULTRASHARP.PTH");

        // Assert
        Assert.Equal("4x-UltraSharp.pth", result);
    }

    [Fact]
    public void FindUpscalerModelName_WithUnavailableName_ReturnsNull()
    {
        // Arrange
        var availableNames = new[] { "4x-UltraSharp.pth" };

        // Act
        var result = ComfyUiResourceHelper.FindUpscalerModelName(availableNames, "unknown.pth");

        // Assert
        Assert.Null(result);
    }
}