using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using Mogri.Models;
using System.Collections.ObjectModel;
using Mogri.Helpers;

namespace Mogri.ViewModels;

public partial class PromptPageViewModel : PageViewModel, IPromptPageViewModel
{
    private readonly IImageGenerationService _stableDiffusionService;
    private readonly IPopupService _popupService;

    private PromptSettings? _settings;

    [ObservableProperty]
    public partial string? Prompt { get; set; }

    [ObservableProperty]
    public partial string? PromptPlaceholder { get; set; }

    [ObservableProperty]
    public partial string? NegativePrompt { get; set; }

    [ObservableProperty]
    public partial List<IPromptStyleViewModel> AvailablePromptStyles { get; set; } = new();

    [ObservableProperty]
    public partial List<ILoraViewModel> AvailableLoras { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<ILoraViewModel> SelectedLoras { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<IPromptStyleViewModel> SelectedPromptStyles { get; set; } = new();

    public PromptPageViewModel(
        IImageGenerationService stableDiffusionService,
        ILoadingService loadingService,
        IPopupService popupService) : base(loadingService)
    {
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
    }

    [RelayCommand]
    private void ResetPage()
    {
        Prompt = string.Empty;
        NegativePrompt = string.Empty;
        SelectedPromptStyles.Clear();
        SelectedLoras.Clear();
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

        if (_settings == null)
        {
            return;
        }

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

                        foreach (var lora in SelectedLoras.Where(l => !matchingLoras.Any(ml => ml.Name == l.Name)))
                        {
                            Shell.Current.Dispatcher.Dispatch(() =>
                            {
                                SelectedLoras.Remove(lora);
                            });
                        }

                        foreach (var lora in matchingLoras.Where(l => !SelectedLoras.Any(sl => sl.Name == l.Name)))
                        {
                            Shell.Current.Dispatcher.Dispatch(() =>
                            {
                                SelectedLoras.Add(lora);
                            });
                        }
                    }
                }));
        }
        catch (Exception ex)
        {
            await _popupService.DisplayAlertAsync("Error", $"Failed to load settings: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private void RemovePromptStyle(IPromptStyleViewModel promptStyleViewModel)
    {
        SelectedPromptStyles.Remove(promptStyleViewModel);

        if (_settings != null)
        {
            _settings.PromptStyles = SelectedPromptStyles.Distinct().ToList();
        }
    }

    [RelayCommand]
    private void RemoveLora(ILoraViewModel loraViewModel)
    {
        SelectedLoras.Remove(loraViewModel);

        if (_settings != null)
        {
            _settings.Loras = SelectedLoras.Distinct().ToList();
        }
    }

    [RelayCommand]
    private async Task ShowPromptStyleCreationPrompt()
    {
        var accepted = await Shell.Current.DisplayAlertAsync("Create Style?", "Would you like to create a style using the existing prompts?", "OK", "Cancel");

        if (accepted)
        {
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

            var combinedPromptAndStyles = SettingsHelper.GetCombinedPromptAndPromptStyles(Prompt ?? string.Empty, NegativePrompt ?? string.Empty, SelectedPromptStyles.Distinct().ToList());

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

        if (_settings == null) return;

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

        if (_settings == null) return;

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

        if (_settings == null) return;

        var parameters = new Dictionary<string, object>()
        {
            { NavigationParams.PromptSettings, _settings }
        };

        await Shell.Current.GoToAsync("..", parameters);
    }

    private void SetPromptsOnSettings()
    {
        if (_settings == null) return;
        _settings.Prompt = Prompt ?? string.Empty;
        _settings.NegativePrompt = NegativePrompt ?? string.Empty;
    }

    public override bool OnBackButtonPressed()
    {
        ConfirmCommand.Execute(null);

        return true;
    }

    private void mapSettingsToProperties()
    {
        if (_settings == null) return;

        Prompt = _settings.Prompt != PromptPlaceholder ? _settings.Prompt : string.Empty;
        NegativePrompt = _settings.NegativePrompt;
    }
}
