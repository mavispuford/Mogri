using Android.Webkit;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using System.Collections.ObjectModel;
using MobileDiffusion.Helpers;

namespace MobileDiffusion.ViewModels;

public partial class PromptPageViewModel : PageViewModel, IPromptPageViewModel
{
    private readonly IStableDiffusionService _stableDiffusionService;

    private Settings _settings;

    [ObservableProperty]
    private string _prompt;

    [ObservableProperty]
    private string _promptPlaceholder;

    [ObservableProperty]
    private string _negativePrompt;

    [ObservableProperty]
    private List<IPromptStyleViewModel> _availablePromptStyles = new();

    [ObservableProperty]
    private ObservableCollection<IPromptStyleViewModel> _selectedPromptStyles = new();

    public PromptPageViewModel(IStableDiffusionService stableDiffusionService)
    {
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) &&
            promptSettings is Settings settings)
        {
            _settings = settings.Clone();
        }
        else
        {
            throw new ArgumentException(nameof(NavigationParams.PromptSettings));
        }

        if (query.TryGetValue(NavigationParams.PromptPlaceholder, out var promptPlaceholder))
        {
            PromptPlaceholder = promptPlaceholder.ToString();
        }

        mapSettingsToProperties();

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    public override async void OnNavigatedTo()
    {
        base.OnNavigatedTo();

        try
        {
            AvailablePromptStyles = await _stableDiffusionService.GetPromptStylesAsync();

            if (_settings.PromptStyles?.Any() == true)
            {
                var matchingStyles = AvailablePromptStyles.SelectMany(a => _settings.PromptStyles.Where(p => p.Name.Equals(a.Name, StringComparison.Ordinal)));

                SelectedPromptStyles = new ObservableCollection<IPromptStyleViewModel>(matchingStyles);
            }
        }
        catch
        {
            // TODO - Handle this
        }
    }

    [RelayCommand]
    private void RemovePromptStyle(IPromptStyleViewModel promptStyleViewModel)
    {
        SelectedPromptStyles.Remove(promptStyleViewModel);

        _settings.PromptStyles = SelectedPromptStyles.Distinct().Select(ps => ps as PromptStyleViewModel).ToList();
    }

    [RelayCommand]
    private async Task ShowPromptStyleCreationPrompt()
    {
        var accepted = await Shell.Current.DisplayAlert("Create Style?", "Would you like to create a style using the existing prompts?", "OK", "Cancel");

        if (accepted)
        {
            // TODO - Create style

            //await _stableDiffusionService.CreatePromptStyleAsync();
        }
    }

    [RelayCommand]
    private async Task ShowPromptStyleExtractionPrompt()
    {
        var accepted = await Shell.Current.DisplayAlert("Extract Style?", "Would you like to extract the prompts from the selected styles?", "OK", "Cancel");

        if (accepted && SelectedPromptStyles.Any())
        {
            if (string.IsNullOrEmpty(Prompt))
            {
                Prompt = PromptPlaceholder;
            }

            var combinedPromptAndStyles = SettingsHelper.GetCombinedPromptAndPromptStyles(Prompt, NegativePrompt, SelectedPromptStyles.Distinct().Select(ps => ps as PromptStyleViewModel).ToList());

            Prompt = combinedPromptAndStyles.Prompt;
            NegativePrompt = combinedPromptAndStyles.NegativePrompt;

            // Calling ToList() to create a copy so we can remove from SelectedPromptStyles while we iterate
            foreach (var style in SelectedPromptStyles.ToList())
            {
                RemovePromptStyle(style);
            }
        }
    }

    [RelayCommand]
    private async Task ShowPromptStyleSelectionPage()
    {
        var parameters = new Dictionary<string, object>()
        {
            {NavigationParams.PromptSettings, _settings.Clone() }
        };

        await Shell.Current.GoToAsync("PromptStyleSelectionPage", parameters);
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
