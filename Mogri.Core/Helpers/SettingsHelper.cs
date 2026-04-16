using Mogri.Interfaces.ViewModels;
using Mogri.Models;
using Mogri.ViewModels;

namespace Mogri.Helpers;

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
                    if (!string.IsNullOrEmpty(prompt) && !prompt.EndsWith(", ") && !prompt.EndsWith(","))
                    {
                        prompt += ", ";
                    }

                    prompt += style.Prompt.TrimStart(',', ' ');
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
                    if (!string.IsNullOrEmpty(negativePrompt) && !negativePrompt.EndsWith(", ") && !negativePrompt.EndsWith(","))
                    {
                        negativePrompt += ", ";
                    }

                    negativePrompt += style.NegativePrompt.TrimStart(',', ' ');
                }
            }
        }

        return (prompt, negativePrompt);
    }
}
