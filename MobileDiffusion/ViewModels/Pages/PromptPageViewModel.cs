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

    private PromptSettings _settings;

    [ObservableProperty]
    private string _prompt;

    [ObservableProperty]
    private string _promptPlaceholder;

    [ObservableProperty]
    private string _negativePrompt;

    [ObservableProperty]
    private List<IPromptStyleViewModel> _availablePromptStyles = new();

    [ObservableProperty]
    private List<ILoraViewModel> _availableLoras = new();

    [ObservableProperty]
    private ObservableCollection<ILoraViewModel> _selectedLoras = new();

    [ObservableProperty]
    private ObservableCollection<IPromptStyleViewModel> _selectedPromptStyles = new();

    public PromptPageViewModel(
        IStableDiffusionService stableDiffusionService,
        ILoadingService loadingService) : base(loadingService)
    {
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(NavigationParams.PromptSettings, out var promptSettings) &&
            promptSettings is PromptSettings settings)
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

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        try
        {
            await Task.WhenAll(
                Task.Run(async () =>
                {
                    AvailablePromptStyles = await _stableDiffusionService.GetPromptStylesAsync();

                    if (_settings.PromptStyles?.Any() == true)
                    {
                        var matchingStyles = AvailablePromptStyles.SelectMany(a => _settings.PromptStyles.Where(p => p.Name.Equals(a.Name, StringComparison.Ordinal)));

                        foreach (var style in SelectedPromptStyles.Where(l => !matchingStyles.Any(ms => ms.Name == l.Name)))
                        {
                            Shell.Current.Dispatcher.Dispatch(() =>
                            {
                                SelectedPromptStyles.Remove(style);
                            });
                        }

                        foreach (var style in matchingStyles.Where(s => !SelectedPromptStyles.Any(sl => sl.Name == s.Name)))
                        {
                            Shell.Current.Dispatcher.Dispatch(() =>
                            {
                                SelectedPromptStyles.Add(style);
                            });
                        }
                    }
                }),
                Task.Run(async () =>
                {
                    AvailableLoras = await _stableDiffusionService.GetLorasAsync();

                    if (_settings.Loras?.Any() == true)
                    {
                        var matchingLoras = AvailableLoras.SelectMany(a => _settings.Loras.Where(p => p.Name.Equals(a.Name, StringComparison.Ordinal)));

                        foreach(var lora in SelectedLoras.Where(l => !matchingLoras.Any(ml => ml.Name == l.Name)))
                        {
                            Shell.Current.Dispatcher.Dispatch(() =>
                            {
                                SelectedLoras.Remove(lora);
                            });
                        }

                        foreach(var lora in matchingLoras.Where(l => !SelectedLoras.Any(sl => sl.Name == l.Name)))
                        {
                            Shell.Current.Dispatcher.Dispatch(() =>
                            {
                                SelectedLoras.Add(lora);
                            });
                        }
                    }
                }));
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
    private void RemoveLora(ILoraViewModel loraViewModel)
    {
        SelectedLoras.Remove(loraViewModel);

        _settings.Loras = SelectedLoras.Distinct().Select(l => l as LoraViewModel).ToList();
    }

    [RelayCommand]
    private async Task ShowPromptStyleCreationPrompt()
    {
        var accepted = await Shell.Current.DisplayAlertAsync("Create Style?", "Would you like to create a style using the existing prompts?", "OK", "Cancel");

        if (accepted)
        {
            // TODO - Create style - Automatic1111 APIs don't currently support this

            //await _stableDiffusionService.CreatePromptStyleAsync();
        }
    }

    [RelayCommand]
    private async Task ShowPromptStyleExtractionPrompt()
    {
        var accepted = await Shell.Current.DisplayAlertAsync("Extract Style?", "Would you like to extract the prompts from the selected styles?", "OK", "Cancel");

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
    private async Task ShowLoraSelectionPage()
    {
        SetPromptsOnSettings();

        var parameters = new Dictionary<string, object>()
        {
            {NavigationParams.PromptSettings, _settings.Clone() }
        };

        await Shell.Current.GoToAsync("LoraSelectionPage", parameters);
    }

    [RelayCommand]
    private async Task ShowPromptStyleSelectionPage()
    {
        SetPromptsOnSettings();

        var parameters = new Dictionary<string, object>()
        {
            {NavigationParams.PromptSettings, _settings.Clone() }
        };

        await Shell.Current.GoToAsync("PromptStyleSelectionPage", parameters);
    }

    [RelayCommand]
    private async Task Confirm()
    {
        SetPromptsOnSettings();

        var parameters = new Dictionary<string, object>()
        {
            { NavigationParams.PromptSettings, _settings }
        };

        await Shell.Current.GoToAsync("..", parameters);
    }

    private void SetPromptsOnSettings()
    {
        _settings.Prompt = Prompt;
        _settings.NegativePrompt = NegativePrompt;
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
