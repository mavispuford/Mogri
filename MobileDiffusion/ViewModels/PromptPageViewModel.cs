using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;

namespace MobileDiffusion.ViewModels;

public partial class PromptPageViewModel : PageViewModel, IPromptPageViewModel
{
    private Settings _settings;

    [ObservableProperty]
    private string _prompt;

    [ObservableProperty]
    private string _promptPlaceholder;

    [ObservableProperty]
    private string _negativePrompt;

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) ||
            promptSettings is not Settings settings)
        {
            throw new ArgumentException(nameof(NavigationParams.PromptSettings));
        }

        if (query.TryGetValue(NavigationParams.PromptPlaceholder, out var promptPlaceholder))
        {
            PromptPlaceholder = promptPlaceholder.ToString();
        }

        _settings = settings.Clone();

        mapSettingsToProperties();

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    [RelayCommand]
    private async Task Confirm()
    {
        _settings.Prompt = Prompt;
        _settings.NegativePrompt = NegativePrompt;

        var parameters = new Dictionary<string, object>()
        {
            { NavigationParams.PromptSettings, _settings }
        };

        await Shell.Current.GoToAsync("..", parameters);
    }

    public override bool OnBackButtonPressed()
    {
        ConfirmCommand.Execute(null);

        return true;
    }

    private void mapSettingsToProperties()
    {
        Prompt = _settings.Prompt;
        NegativePrompt = _settings.NegativePrompt;
    }
}
