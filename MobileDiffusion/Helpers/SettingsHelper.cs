using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using MobileDiffusion.ViewModels;

namespace MobileDiffusion.Helpers;

public static class SettingsHelper
{
    private const string PromptInsertionConstant = "{prompt}";

    public static (string Prompt, string NegativePrompt) GetCombinedPromptAndPromptStyles(this PromptSettings settings)
    {
        return GetCombinedPromptAndPromptStyles(settings.Prompt, settings.NegativePrompt, settings.PromptStyles);
    }

    public static (string Prompt, string NegativePrompt) GetCombinedPromptAndPromptStyles(string prompt, string negativePrompt, IEnumerable<IPromptStyleViewModel> promptStyles)
    {
        if (promptStyles == null)
        {
            return (prompt, negativePrompt);
        }

        foreach (var style in promptStyles)
        {
            if (!string.IsNullOrEmpty(style.Prompt))
            {
                prompt ??= string.Empty;

                if (style.Prompt.Contains(PromptInsertionConstant))
                {
                    prompt = style.Prompt.Replace(PromptInsertionConstant, prompt);
                }
                else
                {
                    if (!prompt.EndsWith(", ") || !prompt.EndsWith(","))
                    {
                        prompt += ", ";
                    }

                    prompt += style.Prompt;
                }
            }

            if (!string.IsNullOrEmpty(style.NegativePrompt))
            {
                negativePrompt ??= string.Empty;

                if (style.NegativePrompt.Contains(PromptInsertionConstant))
                {
                    negativePrompt = style.NegativePrompt.Replace(PromptInsertionConstant, negativePrompt);
                }
                else
                {
                    if (!negativePrompt.EndsWith(", ") || !negativePrompt.EndsWith(","))
                    {
                        negativePrompt += ", ";
                    }

                    negativePrompt += style.NegativePrompt;
                }
            }
        }

        return (prompt, negativePrompt);
    }
}
