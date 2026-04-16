using Moq;
using Mogri.Helpers;
using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Xunit;

namespace Mogri.Tests.Helpers;

public class SettingsHelperTests
{
    [Fact]
    public void NoStyles_ReturnsOriginalPrompts()
    {
        var result = SettingsHelper.GetCombinedPromptAndPromptStyles(
            "cat",
            "blur",
            Enumerable.Empty<IPromptStyleViewModel>());

        Assert.Equal("cat", result.Prompt);
        Assert.Equal("blur", result.NegativePrompt);
    }

    [Fact]
    public void NullStyleList_ReturnsOriginalPrompts()
    {
        var result = SettingsHelper.GetCombinedPromptAndPromptStyles("cat", "blur", null!);

        Assert.Equal("cat", result.Prompt);
        Assert.Equal("blur", result.NegativePrompt);
    }

    [Fact]
    public void StyleWithPlaceholder_InterpolatesPrompt()
    {
        var style = CreateStyle(prompt: "a photo of {prompt}, 4k");

        var result = SettingsHelper.GetCombinedPromptAndPromptStyles(
            "a cat",
            string.Empty,
            [style.Object]);

        Assert.Equal("a photo of a cat, 4k", result.Prompt);
    }

    [Fact]
    public void StyleWithoutPlaceholder_AppendsWithComma()
    {
        var style = CreateStyle(prompt: "high quality");

        var result = SettingsHelper.GetCombinedPromptAndPromptStyles(
            "a cat",
            string.Empty,
            [style.Object]);

        Assert.Equal("a cat, high quality", result.Prompt);
    }

    [Fact]
    public void StyleWithLeadingComma_TrimsBeforeAppending()
    {
        var style = CreateStyle(prompt: ", high quality");

        var result = SettingsHelper.GetCombinedPromptAndPromptStyles(
            "a cat",
            string.Empty,
            [style.Object]);

        Assert.Equal("a cat, high quality", result.Prompt);
    }

    [Fact]
    public void MultipleStyles_AppliedInOrder()
    {
        var first = CreateStyle(prompt: "a photo of {prompt}");
        var second = CreateStyle(prompt: "high quality");

        var result = SettingsHelper.GetCombinedPromptAndPromptStyles(
            "a cat",
            string.Empty,
            [first.Object, second.Object]);

        Assert.Equal("a photo of a cat, high quality", result.Prompt);
    }

    [Fact]
    public void EmptyPrompt_StyleAppendsCleanly()
    {
        var style = CreateStyle(prompt: "high quality");

        var result = SettingsHelper.GetCombinedPromptAndPromptStyles(
            string.Empty,
            string.Empty,
            [style.Object]);

        Assert.Equal("high quality", result.Prompt);
    }

    [Fact]
    public void NegativePrompt_SameLogicAsPositive()
    {
        var first = CreateStyle(negativePrompt: "avoid {prompt}");
        var second = CreateStyle(negativePrompt: "blurry");

        var result = SettingsHelper.GetCombinedPromptAndPromptStyles(
            string.Empty,
            "noise",
            [first.Object, second.Object]);

        Assert.Equal("avoid noise, blurry", result.NegativePrompt);
    }

    [Fact]
    public void ExtensionMethod_WorksWithPromptSettings()
    {
        var style = CreateStyle(prompt: "stylized {prompt}", negativePrompt: "avoid {prompt}");
        var settings = new PromptSettings
        {
            Prompt = "a cat",
            NegativePrompt = "noise",
            PromptStyles = [style.Object],
        };

        var result = settings.GetCombinedPromptAndPromptStyles();

        Assert.Equal("stylized a cat", result.Prompt);
        Assert.Equal("avoid noise", result.NegativePrompt);
    }

    private static Mock<IPromptStyleViewModel> CreateStyle(string? prompt = null, string? negativePrompt = null)
    {
        var style = new Mock<IPromptStyleViewModel>();
        style.SetupGet(s => s.Prompt).Returns(prompt ?? string.Empty);
        style.SetupGet(s => s.NegativePrompt).Returns(negativePrompt ?? string.Empty);
        return style;
    }
}
